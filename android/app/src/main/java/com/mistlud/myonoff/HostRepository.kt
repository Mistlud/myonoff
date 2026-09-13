package com.mistlud.myonoff

import android.content.Context
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import android.util.Log
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.delay
import kotlinx.coroutines.withContext
import org.json.JSONException
import org.json.JSONObject
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.HttpURLConnection
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.Socket
import java.net.SocketTimeoutException
import java.net.URL
import kotlin.system.measureTimeMillis

data class HostStatus(
    val hostname: String,
    val smbReady: Boolean,
    val apiVersion: String,
)

data class ProbeResult(
    val state: HostState,
    val snapshot: ProbeSnapshot,
    val agentStatus: HostStatus? = null,
    val checkedAtMillis: Long? = null,
)

private data class AgentProbe(
    val reachable: Boolean,
    val malformed: Boolean = false,
    val accessDenied: Boolean = false,
    val status: HostStatus? = null,
    val latencyMilliseconds: Long? = null,
    val error: String? = null,
)

class HostRepository(private val context: Context) {
    suspend fun probe(settings: ControllerSettings): ProbeResult = coroutineScope {
        if (!settings.hasPrivateHostIp()) {
            val snapshot = ProbeSnapshot(
                networkAvailable = true,
                agentReachable = false,
                smbReachable = false,
                error = "Host IP must be a private IPv4 LAN address.",
            )
            return@coroutineScope ProbeResult(
                HostState.UNKNOWN,
                snapshot,
                checkedAtMillis = System.currentTimeMillis(),
            )
        }

        if (!hasLanConnection()) {
            val snapshot = ProbeSnapshot(
                networkAvailable = false,
                agentReachable = false,
                smbReachable = false,
                error = "Wi-Fi or Ethernet LAN is unavailable.",
            )
            return@coroutineScope ProbeResult(
                HostState.UNKNOWN,
                snapshot,
                checkedAtMillis = System.currentTimeMillis(),
            )
        }

        val agentDeferred = async(Dispatchers.IO) { probeAgent(settings) }
        val smbDeferred = async(Dispatchers.IO) { probeSmb(settings) }
        val agent = agentDeferred.await()
        val directSmb = smbDeferred.await()
        val smbReady = directSmb && (agent.status?.smbReady ?: true)
        val unexpectedHost = agent.status != null &&
            settings.expectedHostname.isNotBlank() &&
            !agent.status.hostname.equals(settings.expectedHostname, ignoreCase = true)

        val snapshot = ProbeSnapshot(
            networkAvailable = true,
            agentReachable = agent.reachable,
            smbReachable = smbReady,
            agentResponseMalformed = agent.malformed,
            unexpectedHost = unexpectedHost,
            accessDenied = agent.accessDenied,
            latencyMilliseconds = agent.latencyMilliseconds,
            error = agent.error,
        )
        ProbeResult(
            classifyHostState(snapshot),
            snapshot,
            agent.status,
            checkedAtMillis = System.currentTimeMillis(),
        )
    }

    suspend fun wake(settings: ControllerSettings) = withContext(Dispatchers.IO) {
        require(settings.hasPrivateBroadcastIp()) { "Broadcast IP must be a private IPv4 LAN address." }
        val packetBytes = createMagicPacket(settings.hostMac)
        val destination = InetAddress.getByName(settings.broadcastIp)
        DatagramSocket().use { socket ->
            socket.broadcast = true
            repeat(3) { attempt ->
                socket.send(DatagramPacket(packetBytes, packetBytes.size, destination, settings.wolPort))
                if (attempt < 2) delay(150)
            }
        }
        Log.i(TAG, "Wake-on-LAN packet burst sent")
    }

    suspend fun sleep(settings: ControllerSettings) = sendControl(settings, "/sleep")

    suspend fun shutdown(settings: ControllerSettings) = sendControl(settings, "/shutdown")

    private suspend fun sendControl(settings: ControllerSettings, route: String) = withContext(Dispatchers.IO) {
        require(settings.hasPrivateHostIp()) { "Host IP must be a private IPv4 LAN address." }
        require(settings.authToken.isNotBlank()) { "Configure the authentication token in Settings first." }

        val connection = openConnection(settings, route).apply {
            requestMethod = "POST"
            doOutput = true
            setFixedLengthStreamingMode(0)
            setRequestProperty("Authorization", "Bearer ${settings.authToken}")
        }
        try {
            connection.outputStream.use { }
            val responseCode = connection.responseCode
            when {
                responseCode == HttpURLConnection.HTTP_UNAUTHORIZED ->
                    throw SecurityException("The Host Agent rejected the authentication token.")
                responseCode !in 200..299 ->
                    error("Host Agent returned HTTP $responseCode.")
            }
            Log.i(TAG, "Authenticated power request accepted: $route")
        } finally {
            connection.disconnect()
        }
    }

    private fun probeAgent(settings: ControllerSettings): AgentProbe {
        val connection = openConnection(settings, "/status")
        var result: AgentProbe? = null
        val elapsed = try {
            measureTimeMillis {
                val responseCode = connection.responseCode
                if (responseCode !in 200..299) {
                    result = AgentProbe(
                        reachable = false,
                        accessDenied = responseCode == HttpURLConnection.HTTP_FORBIDDEN ||
                            responseCode == HttpURLConnection.HTTP_UNAUTHORIZED,
                        error = "Agent returned HTTP $responseCode.",
                    )
                    return@measureTimeMillis
                }

                val body = connection.inputStream.bufferedReader().use { it.readText() }
                val json = JSONObject(body)
                val status = HostStatus(
                    hostname = json.getString("hostname"),
                    smbReady = json.getBoolean("smbReady"),
                    apiVersion = json.optString("apiVersion", ""),
                )
                result = if (status.hostname.isBlank() || status.apiVersion != "1") {
                    AgentProbe(true, malformed = true, error = "Agent returned incompatible status data.")
                } else {
                    AgentProbe(true, status = status)
                }
            }
        } catch (exception: JSONException) {
            result = AgentProbe(true, malformed = true, error = "Agent returned malformed JSON.")
            null
        } catch (exception: Exception) {
            if (exception is InterruptedException) throw exception
            result = AgentProbe(false, error = exception.message ?: exception.javaClass.simpleName)
            null
        } finally {
            connection.disconnect()
        }

        return result!!.copy(latencyMilliseconds = elapsed)
    }

    private fun probeSmb(settings: ControllerSettings): Boolean = try {
        Socket().use { socket ->
            socket.connect(
                InetSocketAddress(settings.hostIp, settings.smbPort),
                settings.requestTimeoutMilliseconds,
            )
            socket.isConnected
        }
    } catch (_: SocketTimeoutException) {
        false
    } catch (_: Exception) {
        false
    }

    private fun openConnection(settings: ControllerSettings, route: String): HttpURLConnection =
        (URL("http://${settings.hostIp}:${settings.agentPort}$route").openConnection() as HttpURLConnection).apply {
            connectTimeout = settings.requestTimeoutMilliseconds
            readTimeout = settings.requestTimeoutMilliseconds
            useCaches = false
        }

    private fun hasLanConnection(): Boolean {
        val manager = context.getSystemService(ConnectivityManager::class.java)
        val network = manager.activeNetwork ?: return false
        val capabilities = manager.getNetworkCapabilities(network) ?: return false
        return capabilities.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) ||
            capabilities.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET)
    }

    private companion object {
        const val TAG = "MyOnOff"
    }
}

package com.mistlud.myonoff

import android.content.Context

data class ControllerSettings(
    val hostIp: String = "192.168.219.104",
    val hostMac: String = "",
    val broadcastIp: String = "192.168.219.255",
    val wolPort: Int = 9,
    val agentPort: Int = 5055,
    val smbPort: Int = 445,
    val smbShare: String = "domination",
    val authToken: String = "",
    val expectedHostname: String = "",
    val pollIntervalSeconds: Int = 3,
    val requestTimeoutMilliseconds: Int = 2_500,
    val startInEasyMode: Boolean = false,
) {
    fun hasPrivateHostIp(): Boolean = isPrivateIpv4(hostIp)

    fun hasPrivateBroadcastIp(): Boolean = isPrivateIpv4(broadcastIp)

    fun validationErrors(): List<String> = buildList {
        if (!hasPrivateHostIp()) add("Host IP must be a private IPv4 LAN address.")
        if (!hasPrivateBroadcastIp()) add("Broadcast IP must be a private IPv4 LAN address.")
        if (runCatching { createMagicPacket(hostMac) }.isFailure) add("Host MAC must contain 6 hexadecimal bytes.")
        if (listOf(wolPort, agentPort, smbPort).any { it !in 1..65_535 }) add("Ports must be between 1 and 65535.")
        if (pollIntervalSeconds !in 1..60) add("Poll interval must be between 1 and 60 seconds.")
        if (requestTimeoutMilliseconds !in 250..30_000) add("Request timeout must be between 250 and 30000 ms.")
    }

    private fun isIpv4(value: String): Boolean {
        val parts = value.split('.')
        return parts.size == 4 && parts.all { part ->
            val number = part.toIntOrNull()
            part.isNotEmpty() && part.all(Char::isDigit) && number != null && number in 0..255
        }
    }

    private fun isPrivateIpv4(value: String): Boolean {
        if (!isIpv4(value)) return false
        val bytes = value.split('.').map { it.toInt() }
        return bytes[0] == 10 ||
            (bytes[0] == 172 && bytes[1] in 16..31) ||
            (bytes[0] == 192 && bytes[1] == 168)
    }
}

class SettingsStore(context: Context) {
    private val preferences = context.getSharedPreferences("myonoff-controller", Context.MODE_PRIVATE)

    fun load(): ControllerSettings = ControllerSettings(
        hostIp = preferences.getString("hostIp", null) ?: "192.168.219.104",
        hostMac = preferences.getString("hostMac", null) ?: "",
        broadcastIp = preferences.getString("broadcastIp", null) ?: "192.168.219.255",
        wolPort = preferences.getInt("wolPort", 9),
        agentPort = preferences.getInt("agentPort", 5055),
        smbPort = preferences.getInt("smbPort", 445),
        smbShare = preferences.getString("smbShare", null) ?: "domination",
        authToken = preferences.getString("authToken", null) ?: "",
        expectedHostname = preferences.getString("expectedHostname", null) ?: "",
        pollIntervalSeconds = preferences.getInt("pollIntervalSeconds", 3),
        requestTimeoutMilliseconds = preferences.getInt("requestTimeoutMilliseconds", 2_500),
        startInEasyMode = preferences.getBoolean("startInEasyMode", false),
    )

    fun save(settings: ControllerSettings) {
        preferences.edit()
            .putString("hostIp", settings.hostIp)
            .putString("hostMac", settings.hostMac)
            .putString("broadcastIp", settings.broadcastIp)
            .putInt("wolPort", settings.wolPort)
            .putInt("agentPort", settings.agentPort)
            .putInt("smbPort", settings.smbPort)
            .putString("smbShare", settings.smbShare)
            .putString("authToken", settings.authToken)
            .putString("expectedHostname", settings.expectedHostname)
            .putInt("pollIntervalSeconds", settings.pollIntervalSeconds)
            .putInt("requestTimeoutMilliseconds", settings.requestTimeoutMilliseconds)
            .putBoolean("startInEasyMode", settings.startInEasyMode)
            .apply()
    }
}

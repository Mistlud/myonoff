package com.mistlud.myonoff

import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import kotlinx.coroutines.delay
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.text.DateFormat
import java.util.Date

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val store = SettingsStore(applicationContext)
        val repository = HostRepository(applicationContext)

        setContent {
            MaterialTheme {
                MyOnOffApp(store, repository)
            }
        }
    }
}

@Composable
private fun MyOnOffApp(store: SettingsStore, repository: HostRepository) {
    val context = androidx.compose.ui.platform.LocalContext.current
    val permissionRequired = Build.VERSION.SDK_INT >= 37
    var permissionGranted by remember {
        mutableStateOf(
            !permissionRequired ||
                context.checkSelfPermission(Manifest.permission.ACCESS_LOCAL_NETWORK) ==
                PackageManager.PERMISSION_GRANTED,
        )
    }
    val permissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestPermission(),
    ) { granted -> permissionGranted = granted }

    var settings by remember { mutableStateOf(store.load()) }
    var easyMode by remember { mutableStateOf(settings.startInEasyMode) }
    var probe by remember {
        mutableStateOf(
            ProbeResult(
                HostState.UNKNOWN,
                ProbeSnapshot(false, agentReachable = false, smbReachable = false, error = "Not checked yet."),
            ),
        )
    }
    var busy by remember { mutableStateOf(false) }
    var lastActionMessage by remember { mutableStateOf("No power action yet.") }
    var showSettings by remember { mutableStateOf(false) }
    var confirmShutdown by remember { mutableStateOf(false) }
    val scope = rememberCoroutineScope()

    fun refresh() {
        if (busy || !permissionGranted) return
        scope.launch {
            probe = runCatching { repository.probe(settings) }.getOrElse { error ->
                ProbeResult(
                    HostState.UNKNOWN,
                    ProbeSnapshot(
                        networkAvailable = true,
                        agentReachable = false,
                        smbReachable = false,
                        error = error.message ?: "Status check failed.",
                    ),
                    checkedAtMillis = System.currentTimeMillis(),
                )
            }
        }
    }

    fun runAction(
        state: HostState,
        action: suspend () -> Unit,
        acceptedMessage: String,
        timeoutSeconds: Int,
        isComplete: (ProbeResult) -> Boolean,
    ) {
        if (busy || !permissionGranted) return
        busy = true
        lastActionMessage = acceptedMessage
        probe = probe.copy(state = state, snapshot = probe.snapshot.copy(error = acceptedMessage))
        scope.launch {
            try {
                action()
                probe = probe.copy(snapshot = probe.snapshot.copy(error = acceptedMessage))
                val deadline = System.currentTimeMillis() + timeoutSeconds * 1_000L
                var completed = false
                while (System.currentTimeMillis() < deadline) {
                    delay(1_500)
                    val current = repository.probe(settings)
                    if (isComplete(current)) {
                        probe = current
                        lastActionMessage = "Power transition completed: ${current.state.name.replace('_', ' ')}."
                        completed = true
                        break
                    }
                    probe = current.copy(state = state, snapshot = current.snapshot.copy(error = acceptedMessage))
                }
                if (!completed) {
                    val current = repository.probe(settings)
                    probe = current.copy(
                        snapshot = current.snapshot.copy(
                            error = "Transition timed out after $timeoutSeconds seconds. Review current signals and logs.",
                        ),
                    )
                    lastActionMessage = "Transition timed out after $timeoutSeconds seconds."
                }
            } catch (error: Exception) {
                if (error is CancellationException) throw error
                probe = ProbeResult(
                    HostState.UNKNOWN,
                    probe.snapshot.copy(error = error.message ?: "Power action failed."),
                )
                lastActionMessage = "Power action failed: ${error.message ?: error.javaClass.simpleName}"
            } finally {
                busy = false
            }
        }
    }

    LaunchedEffect(permissionRequired) {
        if (permissionRequired && !permissionGranted) {
            permissionLauncher.launch(Manifest.permission.ACCESS_LOCAL_NETWORK)
        }
    }

    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) {
        permissionGranted = !permissionRequired ||
            context.checkSelfPermission(Manifest.permission.ACCESS_LOCAL_NETWORK) ==
            PackageManager.PERMISSION_GRANTED
    }

    LaunchedEffect(settings, permissionGranted, busy) {
        while (isActive) {
            if (permissionGranted && !busy) {
                probe = runCatching { repository.probe(settings) }.getOrElse { error ->
                    ProbeResult(
                        HostState.UNKNOWN,
                        ProbeSnapshot(
                            networkAvailable = true,
                            agentReachable = false,
                            smbReachable = false,
                            error = error.message ?: "Status check failed.",
                        ),
                        checkedAtMillis = System.currentTimeMillis(),
                    )
                }
            }
            delay(settings.pollIntervalSeconds.coerceIn(1, 60) * 1_000L)
        }
    }

    val onWake = {
        val errors = settings.validationErrors()
        if (errors.isEmpty()) {
            runAction(
                HostState.BOOTING,
                { repository.wake(settings) },
                "Wake-on-LAN packet sent; waiting for Agent and SMB.",
                timeoutSeconds = 90,
                isComplete = { it.state == HostState.ONLINE },
            )
        } else {
            probe = probe.copy(state = HostState.UNKNOWN, snapshot = probe.snapshot.copy(error = errors.joinToString(" ")))
        }
    }

    if (easyMode) {
        EasyModeScreen(
            state = probe.state,
            busy = busy,
            permissionGranted = permissionGranted,
            onRequestPermission = { permissionLauncher.launch(Manifest.permission.ACCESS_LOCAL_NETWORK) },
            onWake = onWake,
            onExit = { easyMode = false },
        )
    } else {
        MainScreen(
            settings = settings,
            probe = probe,
            busy = busy,
            lastActionMessage = lastActionMessage,
            permissionGranted = permissionGranted,
            onRequestPermission = { permissionLauncher.launch(Manifest.permission.ACCESS_LOCAL_NETWORK) },
            onRefresh = ::refresh,
            onSettings = { showSettings = true },
            onEasyMode = { easyMode = true },
            onWake = onWake,
            onSleep = {
                runAction(
                    HostState.GOING_TO_SLEEP,
                    { repository.sleep(settings) },
                    "Sleep request accepted; waiting for the host to become unreachable.",
                    timeoutSeconds = 45,
                    isComplete = { it.state == HostState.OFFLINE },
                )
            },
            onShutdown = { confirmShutdown = true },
        )
    }

    if (showSettings) {
        SettingsDialog(
            initial = settings,
            onDismiss = { showSettings = false },
            onSave = { updated ->
                store.save(updated)
                settings = updated
                showSettings = false
            },
        )
    }

    if (confirmShutdown) {
        AlertDialog(
            onDismissRequest = { confirmShutdown = false },
            title = { Text("Confirm shutdown") },
            text = { Text("Shut down the Host PC normally?") },
            confirmButton = {
                TextButton(onClick = {
                    confirmShutdown = false
                    runAction(
                        HostState.SHUTTING_DOWN,
                        { repository.shutdown(settings) },
                        "Shutdown request accepted; waiting for the host to become unreachable.",
                        timeoutSeconds = 45,
                        isComplete = { it.state == HostState.OFFLINE },
                    )
                }) { Text("Shutdown") }
            },
            dismissButton = {
                TextButton(onClick = { confirmShutdown = false }) { Text("Cancel") }
            },
        )
    }
}

@Composable
private fun MainScreen(
    settings: ControllerSettings,
    probe: ProbeResult,
    busy: Boolean,
    lastActionMessage: String,
    permissionGranted: Boolean,
    onRequestPermission: () -> Unit,
    onRefresh: () -> Unit,
    onSettings: () -> Unit,
    onEasyMode: () -> Unit,
    onWake: () -> Unit,
    onSleep: () -> Unit,
    onShutdown: () -> Unit,
) {
    val statusColor = when (probe.state) {
        HostState.ONLINE -> Color(0xFF12B76A)
        HostState.BOOTING -> Color(0xFFF79009)
        HostState.OFFLINE -> Color(0xFF667085)
        HostState.GOING_TO_SLEEP, HostState.SHUTTING_DOWN -> Color(0xFF2E90FA)
        HostState.UNKNOWN -> Color(0xFFF04438)
    }

    Column(
        modifier = Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            Text("Host PC", fontSize = 28.sp, fontWeight = FontWeight.SemiBold)
            Row {
                TextButton(onClick = onEasyMode, enabled = !busy) { Text("Easy Mode") }
                TextButton(onClick = onSettings, enabled = !busy) { Text("Settings") }
            }
        }
        Spacer(Modifier.height(26.dp))
        Text("● ${probe.state.name.replace('_', ' ')}", color = statusColor, fontSize = 30.sp, fontWeight = FontWeight.Bold)
        Text(settings.hostIp, color = Color(0xFF667085), modifier = Modifier.padding(top = 5.dp))

        Card(modifier = Modifier.fillMaxWidth().padding(vertical = 24.dp)) {
            Column(Modifier.padding(18.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                DetailRow("Agent", if (probe.snapshot.agentReachable) "Ready" else "Unavailable")
                DetailRow("SMB", if (probe.snapshot.smbReachable) "Ready (${settings.smbShare})" else "Unavailable")
                DetailRow("Latency", probe.snapshot.latencyMilliseconds?.let { "$it ms" } ?: "—")
                DetailRow(
                    "Last check",
                    probe.checkedAtMillis?.let { DateFormat.getTimeInstance(DateFormat.MEDIUM).format(Date(it)) }
                        ?: "Not checked yet",
                )
                Text(probe.snapshot.error ?: describeState(probe.state), color = Color(0xFF475467), fontSize = 13.sp)
                Text("Last action: $lastActionMessage", color = Color(0xFF667085), fontSize = 12.sp)
            }
        }

        if (!permissionGranted) {
            Text("Local-network permission is required for LAN control.", color = Color(0xFFF04438))
            OutlinedButton(onClick = onRequestPermission) { Text("Grant LAN permission") }
        }

        Button(
            onClick = onWake,
            enabled = !busy && permissionGranted,
            modifier = Modifier.fillMaxWidth().height(58.dp),
            colors = ButtonDefaults.buttonColors(containerColor = Color(0xFF067647)),
        ) { Text("ON", fontSize = 18.sp, fontWeight = FontWeight.Bold) }
        Spacer(Modifier.height(12.dp))
        Button(
            onClick = onSleep,
            enabled = !busy && permissionGranted && probe.snapshot.agentReachable &&
                !probe.snapshot.agentResponseMalformed && !probe.snapshot.unexpectedHost &&
                !probe.snapshot.accessDenied && settings.authToken.isNotBlank(),
            modifier = Modifier.fillMaxWidth().height(58.dp),
            colors = ButtonDefaults.buttonColors(containerColor = Color(0xFFB54708)),
        ) { Text("Sleep", fontSize = 18.sp, fontWeight = FontWeight.Bold) }
        Spacer(Modifier.height(12.dp))
        Button(
            onClick = onShutdown,
            enabled = !busy && permissionGranted && probe.snapshot.agentReachable &&
                !probe.snapshot.agentResponseMalformed && !probe.snapshot.unexpectedHost &&
                !probe.snapshot.accessDenied && settings.authToken.isNotBlank(),
            modifier = Modifier.fillMaxWidth().height(58.dp),
            colors = ButtonDefaults.buttonColors(containerColor = Color(0xFFB42318)),
        ) { Text("Shutdown", fontSize = 18.sp, fontWeight = FontWeight.Bold) }

        TextButton(onClick = onRefresh, enabled = !busy && permissionGranted) { Text("Refresh status") }
    }
}

@Composable
private fun EasyModeScreen(
    state: HostState,
    busy: Boolean,
    permissionGranted: Boolean,
    onRequestPermission: () -> Unit,
    onWake: () -> Unit,
    onExit: () -> Unit,
) {
    val effectiveState = if (permissionGranted) state else HostState.UNKNOWN
    val presentation = easyModePresentation(effectiveState)
    val statusColor = when (effectiveState) {
        HostState.ONLINE -> Color(0xFF12B76A)
        HostState.BOOTING -> Color(0xFFF79009)
        HostState.OFFLINE -> Color(0xFF667085)
        else -> Color(0xFF98A2B3)
    }

    Column(
        modifier = Modifier.fillMaxSize().padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            Text("MyOnOff", fontSize = 20.sp, fontWeight = FontWeight.SemiBold)
            TextButton(onClick = onExit) { Text("Back to Details") }
        }
        Spacer(Modifier.weight(1f))
        Text("●", color = statusColor, fontSize = 34.sp)
        Text(presentation.status, fontSize = 34.sp, fontWeight = FontWeight.Bold, modifier = Modifier.padding(top = 12.dp))
        Text(
            if (permissionGranted) presentation.detail else "Local-network access is required.",
            color = Color(0xFF667085),
            fontSize = 16.sp,
            modifier = Modifier.padding(top = 10.dp),
        )
        if (presentation.showProgress) {
            CircularProgressIndicator(modifier = Modifier.padding(top = 28.dp))
        }
        if (presentation.showOnButton) {
            Button(
                onClick = onWake,
                enabled = !busy,
                modifier = Modifier.fillMaxWidth().padding(top = 24.dp).height(92.dp),
                colors = ButtonDefaults.buttonColors(containerColor = Color(0xFF067647)),
            ) { Text("ON", fontSize = 28.sp, fontWeight = FontWeight.Bold) }
        }
        if (!permissionGranted) {
            OutlinedButton(onClick = onRequestPermission, modifier = Modifier.padding(top = 24.dp)) {
                Text("Allow local network")
            }
        }
        Spacer(Modifier.weight(1f))
        Text("Version ${BuildConfig.VERSION_NAME}", color = Color(0xFF98A2B3), fontSize = 11.sp)
    }
}

internal data class EasyModePresentation(
    val status: String,
    val detail: String,
    val showProgress: Boolean,
    val showOnButton: Boolean,
)

internal fun easyModePresentation(state: HostState): EasyModePresentation = when (state) {
    HostState.ONLINE -> EasyModePresentation("ON", "Host is ready.", showProgress = false, showOnButton = false)
    HostState.BOOTING -> EasyModePresentation(
        "Turning on",
        "Please wait while the host becomes ready.",
        showProgress = true,
        showOnButton = false,
    )
    HostState.OFFLINE -> EasyModePresentation(
        "Host is off",
        "Press ON to wake the host.",
        showProgress = false,
        showOnButton = true,
    )
    else -> EasyModePresentation(
        "Checking status",
        "Checking the local network.",
        showProgress = false,
        showOnButton = false,
    )
}

@Composable
private fun DetailRow(label: String, value: String) {
    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
        Text(label, fontWeight = FontWeight.SemiBold)
        Text(value)
    }
}

@Composable
private fun SettingsDialog(
    initial: ControllerSettings,
    onDismiss: () -> Unit,
    onSave: (ControllerSettings) -> Unit,
) {
    var hostIp by remember { mutableStateOf(initial.hostIp) }
    var hostMac by remember { mutableStateOf(initial.hostMac) }
    var broadcastIp by remember { mutableStateOf(initial.broadcastIp) }
    var wolPort by remember { mutableStateOf(initial.wolPort.toString()) }
    var agentPort by remember { mutableStateOf(initial.agentPort.toString()) }
    var smbPort by remember { mutableStateOf(initial.smbPort.toString()) }
    var smbShare by remember { mutableStateOf(initial.smbShare) }
    var expectedHostname by remember { mutableStateOf(initial.expectedHostname) }
    var authToken by remember { mutableStateOf(initial.authToken) }
    var startInEasyMode by remember { mutableStateOf(initial.startInEasyMode) }
    var errorText by remember { mutableStateOf("") }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("LAN Settings") },
        text = {
            Column(Modifier.verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                OutlinedTextField(hostIp, { hostIp = it }, label = { Text("Host IP") }, singleLine = true)
                OutlinedTextField(hostMac, { hostMac = it }, label = { Text("Host MAC") }, singleLine = true)
                OutlinedTextField(broadcastIp, { broadcastIp = it }, label = { Text("Broadcast IP") }, singleLine = true)
                OutlinedTextField(wolPort, { wolPort = it }, label = { Text("WOL port") }, singleLine = true)
                OutlinedTextField(agentPort, { agentPort = it }, label = { Text("Agent port") }, singleLine = true)
                OutlinedTextField(smbPort, { smbPort = it }, label = { Text("SMB port") }, singleLine = true)
                OutlinedTextField(smbShare, { smbShare = it }, label = { Text("SMB share") }, singleLine = true)
                OutlinedTextField(expectedHostname, { expectedHostname = it }, label = { Text("Expected hostname") }, singleLine = true)
                OutlinedTextField(
                    authToken,
                    { authToken = it },
                    label = { Text("Authentication token") },
                    singleLine = true,
                    visualTransformation = PasswordVisualTransformation(),
                )
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Checkbox(checked = startInEasyMode, onCheckedChange = { startInEasyMode = it })
                    Text("Start app in Easy Mode")
                }
                Text("Version ${BuildConfig.VERSION_NAME}", color = Color(0xFF667085), fontSize = 12.sp)
                if (errorText.isNotEmpty()) Text(errorText, color = Color(0xFFF04438), fontSize = 12.sp)
            }
        },
        confirmButton = {
            TextButton(onClick = {
                val candidate = initial.copy(
                    hostIp = hostIp.trim(),
                    hostMac = hostMac.trim(),
                    broadcastIp = broadcastIp.trim(),
                    wolPort = wolPort.toIntOrNull() ?: -1,
                    agentPort = agentPort.toIntOrNull() ?: -1,
                    smbPort = smbPort.toIntOrNull() ?: -1,
                    smbShare = smbShare.trim(),
                    expectedHostname = expectedHostname.trim(),
                    authToken = authToken,
                    startInEasyMode = startInEasyMode,
                )
                val errors = candidate.validationErrors()
                if (errors.isEmpty()) onSave(candidate) else errorText = errors.joinToString(" ")
            }) { Text("Save") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } },
    )
}

private fun describeState(state: HostState): String = when (state) {
    HostState.ONLINE -> "Host Agent and SMB are ready."
    HostState.BOOTING -> "The host is reachable but Agent or SMB is not ready yet."
    HostState.OFFLINE -> "No Host Agent or SMB response was received."
    HostState.UNKNOWN -> "The app cannot confidently classify the host."
    HostState.GOING_TO_SLEEP -> "Waiting for the host to sleep."
    HostState.SHUTTING_DOWN -> "Waiting for the host to shut down."
}

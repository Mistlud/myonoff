package com.mistlud.myonoff

enum class HostState {
    OFFLINE,
    BOOTING,
    ONLINE,
    UNKNOWN,
    GOING_TO_SLEEP,
    SHUTTING_DOWN,
}

data class ProbeSnapshot(
    val networkAvailable: Boolean,
    val pingReachable: Boolean = false,
    val agentReachable: Boolean,
    val smbReachable: Boolean,
    val agentResponseMalformed: Boolean = false,
    val unexpectedHost: Boolean = false,
    val accessDenied: Boolean = false,
    val latencyMilliseconds: Long? = null,
    val error: String? = null,
)

fun classifyHostState(snapshot: ProbeSnapshot): HostState = when {
    !snapshot.networkAvailable || snapshot.agentResponseMalformed || snapshot.unexpectedHost || snapshot.accessDenied -> HostState.UNKNOWN
    snapshot.agentReachable && snapshot.smbReachable -> HostState.ONLINE
    snapshot.pingReachable || snapshot.agentReachable || snapshot.smbReachable -> HostState.BOOTING
    else -> HostState.OFFLINE
}

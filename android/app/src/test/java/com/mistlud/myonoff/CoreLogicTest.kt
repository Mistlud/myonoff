package com.mistlud.myonoff

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertThrows
import org.junit.Test

class CoreLogicTest {
    @Test
    fun magicPacketHasStandardLayout() {
        val packet = createMagicPacket("AA:BB:CC:DD:EE:FF")
        val mac = byteArrayOf(0xAA.toByte(), 0xBB.toByte(), 0xCC.toByte(), 0xDD.toByte(), 0xEE.toByte(), 0xFF.toByte())

        assertEquals(102, packet.size)
        assertArrayEquals(ByteArray(6) { 0xFF.toByte() }, packet.copyOfRange(0, 6))
        repeat(16) { repetition ->
            assertArrayEquals(mac, packet.copyOfRange(6 + repetition * 6, 12 + repetition * 6))
        }
    }

    @Test
    fun invalidMacIsRejected() {
        assertThrows(IllegalArgumentException::class.java) { createMagicPacket("not-a-mac") }
    }

    @Test
    fun statusClassificationMatchesMvpRules() {
        assertEquals(HostState.ONLINE, classifyHostState(ProbeSnapshot(true, true, true, true)))
        assertEquals(HostState.BOOTING, classifyHostState(ProbeSnapshot(true, true, false, false)))
        assertEquals(HostState.OFFLINE, classifyHostState(ProbeSnapshot(true, false, false, false)))
        assertEquals(HostState.UNKNOWN, classifyHostState(ProbeSnapshot(false, false, false, false)))
        assertEquals(
            HostState.UNKNOWN,
            classifyHostState(ProbeSnapshot(true, false, true, true, unexpectedHost = true)),
        )
        assertEquals(
            HostState.UNKNOWN,
            classifyHostState(ProbeSnapshot(true, false, false, false, accessDenied = true)),
        )
    }

    @Test
    fun settingsRejectPublicTargets() {
        assertEquals(
            false,
            ControllerSettings(hostIp = "8.8.8.8", hostMac = "AA:BB:CC:DD:EE:FF").hasPrivateHostIp(),
        )
    }
}

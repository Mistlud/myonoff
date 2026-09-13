package com.mistlud.myonoff

fun createMagicPacket(macAddress: String): ByteArray {
    val normalized = macAddress.replace(Regex("[:-]|\\."), "")
    require(normalized.length == 12) { "MAC address must contain exactly 6 bytes." }

    val mac = ByteArray(6) { index ->
        normalized.substring(index * 2, index * 2 + 2).toIntOrNull(16)?.toByte()
            ?: throw IllegalArgumentException("MAC address contains invalid hexadecimal characters.")
    }

    return ByteArray(102).also { packet ->
        repeat(6) { packet[it] = 0xFF.toByte() }
        repeat(16) { repetition ->
            mac.copyInto(packet, destinationOffset = 6 + repetition * mac.size)
        }
    }
}

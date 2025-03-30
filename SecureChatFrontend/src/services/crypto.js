const KEY_OPTIONS = {
    name: "ECDH",
    namedCurve: "P-256", // NIST P-256 curve
};

const ALG_OPTIONS = {
    name: "AES-GCM",
    length: 256, // Key length for AES
};

// Generate an ECDH key pair for Elliptic Curve Diffie-Hellman
export async function generateKeyPair() {
    return await window.crypto.subtle.generateKey(
        KEY_OPTIONS,
        true, // Can extract the private key (needed for deriveBits/deriveKey)
        ["deriveKey", "deriveBits"] // Key usages
    );
}

// Export a public key to JWK format for sharing
export async function exportPublicKey(key) {
    return await window.crypto.subtle.exportKey("jwk", key);
}

// Import a public key from JWK format
export async function importPublicKey(jwk) {
    return await window.crypto.subtle.importKey(
        "jwk",
        jwk,
        KEY_OPTIONS,
        true, // Doesn't matter much for public keys
        [] // Public key is not used for derivation directly here, only as input
    );
}

// Derive a shared secret (AES-GCM key) using your private key and their public key
export async function deriveSharedKey(privateKey, publicKey) {
    return await window.crypto.subtle.deriveKey(
        {
            name: "ECDH",
            public: publicKey, // The peer's public key
        },
        privateKey, // Your private key
        ALG_OPTIONS, // Algorithm parameters for the derived key (AES-GCM)
        true, // Can extract the derived key (false is usually better for security)
        ["encrypt", "decrypt"] // Usages for the derived key
    );
}

// Encrypt data using AES-GCM
export async function encryptData(key, data) {
    const iv = window.crypto.getRandomValues(new Uint8Array(12)); // GCM standard IV size is 12 bytes
    const encoder = new TextEncoder();
    const encodedData = encoder.encode(data);

    const encryptedContent = await window.crypto.subtle.encrypt(
        {
            name: "AES-GCM",
            iv: iv,
        },
        key,
        encodedData
    );

    // Return IV and ciphertext, typically base64 encoded for transmission
    return {
        iv: bufferToBase64(iv),
        ciphertext: bufferToBase64(encryptedContent),
    };
}

// Decrypt data using AES-GCM
export async function decryptData(key, ivBase64, ciphertextBase64) {
    try {
        const iv = base64ToBuffer(ivBase64);
        const ciphertext = base64ToBuffer(ciphertextBase64);

        const decryptedContent = await window.crypto.subtle.decrypt(
            {
                name: "AES-GCM",
                iv: iv,
            },
            key,
            ciphertext
        );

        const decoder = new TextDecoder();
        return decoder.decode(decryptedContent);
    } catch (e) {
        console.error("Decryption failed:", e);
        // IMPORTANT: Decryption failure often means the data was tampered with (integrity check failed)
        // or the wrong key/IV was used.
        return null; // Indicate failure
    }
}

// Helper functions for Base64 encoding/decoding
function bufferToBase64(buffer) {
    return btoa(String.fromCharCode(...new Uint8Array(buffer)));
}

function base64ToBuffer(base64) {
    const binaryString = atob(base64);
    const len = binaryString.length;
    const bytes = new Uint8Array(len);
    for (let i = 0; i < len; i++) {
        bytes[i] = binaryString.charCodeAt(i);
    }
    return bytes.buffer;
}
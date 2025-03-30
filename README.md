# Secure Chat Mini-Project

## Project Description

This project implements a simple two-person chat application with a focus on **Confidentiality** and **Integrity**. It uses End-to-End Encryption (E2EE) to ensure that only the intended sender and receiver can read messages. The server acts only as a relay for encrypted data and to facilitate user discovery and public key exchange.

The frontend is built with Vue 3 and uses the standard Web Crypto API (`SubtleCrypto`) for cryptographic operations. The backend is an ASP.NET Core application using SignalR for real-time communication and user management.

## Technologies Used

*   **Frontend:** Vue 3 (Composition API), Vite, JavaScript, HTML, CSS
*   **Real-time Communication:** SignalR (`@microsoft/signalr` client library)
*   **Cryptography:** Web Crypto API (`window.crypto.subtle`)
    *   Key Exchange: Elliptic Curve Diffie-Hellman (ECDH) using the P-256 curve.
    *   Encryption: AES-GCM (256-bit key) providing Authenticated Encryption (Confidentiality + Integrity).
*   **Backend:** C#, ASP.NET Core, SignalR

## Setup and Running Instructions

### Prerequisites

*   [.NET SDK](https://dotnet.microsoft.com/download)
*   [Node.js](https://nodejs.org/)

### Steps

1.  **Clone the Repository:**
    ```bash
    git clone https://github.com/koko-puffs/SecureChat
    cd SecureChat
    ```

2.  **Run the Backend Server:**
    ```bash
    cd SecureChatBackend
    dotnet restore # Installs dependencies
    dotnet run
    ```
    The backend server should now be running, typically listening on `http://localhost:5000` and `https://localhost:5001` (check console output for exact ports). The SignalR hub is available at `/chathub`.

3.  **Run the Frontend Application:**
    *   Open a **new terminal window/tab**.
    *   Navigate to the frontend directory:
        ```bash
        cd ../SecureChatFrontend
        npm install # Installs dependencies
        npm run dev # Starts the Vite development server
        ```
    The frontend development server should now be running, typically at `http://localhost:5173`.

4.  **Access the Application:**
    *   Open two separate browser tabs or windows.
    *   Navigate to `http://localhost:5173` (or the port shown by Vite) in both windows.
    *   If you are unable to login, then adjust the port inside signalr.js to be whatever port your backend is running on.

## How to Use

1.  **Login:** In each browser window, enter a **unique username** and click "Login".
2.  **User List:** Once logged in, you should see the list of other online users in the sidebar on the left.
3.  **Select User:** Click on the username of the person you want to chat with in the sidebar.
4.  **Chat:** The chat window for the selected user will appear. The application will automatically perform a key exchange (ECDH) in the background to establish a secure channel. Once the "Establishing secure connection..." message disappears, you can type your message in the input field at the bottom and press Enter or click "Send".
5.  **Receive Messages:** Incoming messages from the selected user will appear in the chat window. Messages you send will appear aligned to the right; messages you receive will appear aligned to the left.

## Cryptography Implementation

This application uses End-to-End Encryption (E2EE) to achieve confidentiality and integrity for messages exchanged between two users.

1.  **Key Generation:**
    *   When the application loads, each client generates a unique **Elliptic Curve Diffie-Hellman (ECDH)** key pair using the P-256 curve (`window.crypto.subtle.generateKey`). This consists of a private key (kept secret by the client) and a public key.

2.  **Registration and Public Key Exchange:**
    *   When a user logs in, they send their `username` and their **public key** (exported to JSON Web Key - JWK format) to the SignalR server.
    *   The server stores the `username`, `ConnectionId`, and `publicKey` for each connected user.
    *   The server broadcasts the `username` and `publicKey` of newly connected users to others, and sends the existing user list to the new user. This allows clients to discover each other's public keys **without the server ever seeing any private keys.**

3.  **Shared Secret Derivation (ECDH):**
    *   When User A wants to chat with User B (or receives the first message from B), User A uses:
        *   Its **own private** ECDH key.
        *   User B's **public** ECDH key (obtained from the server during the user list update).
    *   User A performs an ECDH key derivation (`window.crypto.subtle.deriveKey`) to compute a **shared secret key**.
    *   Simultaneously, User B performs the same derivation using *its* private key and *User A's* public key.
    *   **Crucially, both users arrive at the exact same shared secret key** without ever exchanging the secret itself. The server cannot compute this secret as it only ever sees the public keys.
    *   This derived key is an **AES-GCM key** (256-bit) suitable for symmetric encryption.

4.  **Message Encryption (AES-GCM):**
    *   Before sending a message, the sender encrypts the plaintext using the **derived AES-GCM shared key** specific to the recipient (`window.crypto.subtle.encrypt`).
    *   **AES-GCM** (Galois/Counter Mode) is an **Authenticated Encryption with Associated Data (AEAD)** cipher. This means it provides:
        *   **Confidentiality:** The message content (ciphertext) is unreadable without the shared key.
        *   **Integrity & Authenticity:** It generates an authentication tag based on the key, ciphertext, and a unique Initialization Vector (IV). If the ciphertext or IV is tampered with during transit, decryption will fail, ensuring the receiver knows the message is corrupted or forged.
    *   A unique **Initialization Vector (IV)** (12 bytes, randomly generated) is required for each encryption operation with the same key. This IV is sent along with the ciphertext to the recipient (it doesn't need to be secret).
    *   The IV and ciphertext are typically Base64 encoded before being sent as a JSON payload over SignalR.

5.  **Message Decryption (AES-GCM):**
    *   The recipient receives the JSON payload containing the Base64 encoded IV and ciphertext.
    *   They decode the IV and ciphertext.
    *   Using the **same shared AES-GCM key** (derived via ECDH earlier), they attempt to decrypt the ciphertext using the provided IV (`window.crypto.subtle.decrypt`).
    *   If decryption succeeds, the original plaintext is recovered.
    *   If decryption fails (throws an error), it indicates that the message was tampered with (integrity violation) or the wrong key/IV was used. The application handles this by showing an error message instead of the corrupted text.

### Why This Approach?

*   **E2EE:** Meets the core requirement of preventing eavesdropping, even by the server operator.
*   **Confidentiality:** AES-GCM encryption ensures only those with the shared key can read the message.
*   **Integrity:** AES-GCM's authentication tag prevents undetected modification of messages in transit.
*   **Standard Algorithms:** Uses well-vetted, standard cryptographic primitives (ECDH P-256, AES-GCM) available via the browser's built-in `SubtleCrypto` API, avoiding the pitfalls of rolling your own crypto.
*   **Feasibility:** Achievable within a mini-project scope, demonstrating core E2EE concepts.

### Limitations / Simplifications

*   **No Forward/Backward Secrecy:** The session key derived via ECDH is typically used for the duration of the chat session between two users. If a long-term private key were compromised, an attacker *might* be able to derive past session keys if they had recorded the public key exchange. Protocols like Signal achieve stronger secrecy by frequently rotating keys (e.g., using Double Ratchet), which is beyond the scope here.
*   **Basic Key Management:** Private keys are generated in memory and lost when the browser tab is closed. There's no persistent storage or identity verification beyond the username.
*   **Trust-On-First-Use (TOFU) for Keys:** Users implicitly trust the public key initially received from the server. A Man-in-the-Middle (MitM) attack during the *initial key exchange* is theoretically possible if the connection to the server isn't secure or if the server itself is malicious/compromised (though messages *after* key exchange are secure from the server). Real-world apps use key verification mechanisms (e.g., comparing safety numbers/QR codes).
*   **Server Trust:** Users must trust the server to relay public keys correctly and not substitute them during the exchange.
*   **Metadata:** The server knows *who* is talking to *whom* and *when*, just not *what* they are saying.

### Screenshots

![Screenshot 1](https://raw.githubusercontent.com/koko-puffs/SecureChat/refs/heads/main/screenshot1.png "Selecting your username")
![Screenshot 2](https://raw.githubusercontent.com/koko-puffs/SecureChat/refs/heads/main/screenshot2.png "Chatting with another user")
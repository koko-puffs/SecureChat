<template>
    <div class="chat-container">
      <h2>Secure Chat</h2>
  
      <!-- Login Section -->
      <div v-if="!isLoggedIn">
        <input type="text" v-model="usernameInput" placeholder="Enter username" @keyup.enter="login" />
        <button @click="login" :disabled="!usernameInput.trim()">Login</button>
        <p v-if="loginError" class="error">{{ loginError }}</p>
      </div>
  
      <!-- Chat Section -->
      <div v-if="isLoggedIn" class="chat-area">
        <div class="sidebar">
          <h3>Online Users</h3>
          <ul v-if="users.length > 0">
            <li
              v-for="user in users"
              :key="user.username"
              @click="selectUser(user)"
              :class="{ selected: selectedUser?.username === user.username }"
            >
              {{ user.username }} {{ user.username === username ? '(You)' : '' }}
            </li>
          </ul>
           <p v-else>No other users online.</p>
        </div>
  
        <div class="main-chat">
          <div v-if="selectedUser">
            <h3>Chatting with {{ selectedUser.username }}</h3>
            <div class="messages" ref="messageContainer">
              <div v-for="(msg, index) in currentChatMessages" :key="index" :class="['message', msg.sender === username ? 'sent' : 'received']">
                 <strong>{{ msg.sender }}:</strong> {{ msg.text }}
                 <span class="timestamp">{{ msg.timestamp.toLocaleTimeString() }}</span>
              </div>
            </div>
            <div class="message-input">
              <input
                type="text"
                v-model="newMessage"
                placeholder="Type your message..."
                @keyup.enter="sendMessage"
                :disabled="!selectedUser || !sharedKeys[selectedUser.username]"
              />
              <button @click="sendMessage" :disabled="!newMessage.trim() || !selectedUser || !sharedKeys[selectedUser.username]">
                  Send
              </button>
               <p v-if="selectedUser && !sharedKeys[selectedUser.username]" class="warning">
                  Establishing secure connection with {{selectedUser.username}}...
               </p>
            </div>
          </div>
          <div v-else>
            <p>Select a user to start chatting.</p>
          </div>
        </div>
      </div>
       <p>Status: {{ connectionStatus }}</p>
    </div>
  </template>
  
  <script setup>
  import { ref, reactive, onMounted, onUnmounted, nextTick } from 'vue';
  import { createConnection, getConnection, startConnection, stopConnection } from '../services/signalr';
  import * as cryptoService from '../services/crypto';
  
  const username = ref('');
  const usernameInput = ref('');
  const isLoggedIn = ref(false);
  const loginError = ref('');
  const connectionStatus = ref('Disconnected');
  const users = ref([]); // List of { username: string, publicKey: JWK }
  const selectedUser = ref(null);
  const messages = reactive({}); // Store messages per user: { 'username': [{ sender, text, timestamp }] }
  const currentChatMessages = ref([]); // Messages for the currently selected chat
  const newMessage = ref('');
  const messageContainer = ref(null); // Ref for scrolling
  
  let connection = null;
  let keyPair = null; // Stores own ECDH key pair { publicKey, privateKey }
  const sharedKeys = reactive({}); // Stores derived shared keys: { 'username': CryptoKey }
  
  // --- Lifecycle Hooks ---
  onMounted(async () => {
    connection = createConnection();
    setupSignalRListeners();
    try {
      connectionStatus.value = 'Connecting...';
      await startConnection();
      connectionStatus.value = 'Connected (Not Logged In)';
    } catch (e) {
      connectionStatus.value = 'Connection Failed';
       console.error("Initial connection failed", e);
    }
  
     // Generate key pair on component mount
    try {
        keyPair = await cryptoService.generateKeyPair();
        console.log("ECDH Key pair generated");
    } catch (error) {
        console.error("Failed to generate key pair:", error);
        // Handle error appropriately (e.g., disable login)
        loginError.value = "Crypto setup failed. Cannot login.";
    }
  });
  
  onUnmounted(async () => {
    await stopConnection();
    connectionStatus.value = 'Disconnected';
  });
  
  // --- Methods ---
  async function login() {
    if (!usernameInput.value.trim() || !keyPair || connection.state !== 'Connected') {
        loginError.value = "Cannot login. Ensure username is entered, crypto is ready, and connection is established.";
        return;
    }
    loginError.value = ''; // Clear previous errors
    username.value = usernameInput.value.trim();
  
     try {
         // Export public key to JWK format
         const publicKeyJwk = await cryptoService.exportPublicKey(keyPair.publicKey);
         const publicKeyString = JSON.stringify(publicKeyJwk); // Send as string
  
         // Register with the server
         await connection.invoke("Register", username.value, publicKeyString);
         // Server will either confirm via UpdateUserList or deny via RegistrationFailed
         // We assume success for now and wait for listeners to update state.
         // Note: A dedicated 'RegistrationSuccess' message might be better.
         isLoggedIn.value = true; // Optimistically set logged in
         connectionStatus.value = `Logged in as ${username.value}`;
  
     } catch (error) {
         console.error("Login failed:", error);
         loginError.value = `Login failed: ${error.message || 'Unknown error'}`;
         isLoggedIn.value = false; // Revert optimistic login
         connectionStatus.value = 'Login Failed';
     }
  }
  
  async function selectUser(user) {
    if (user.username === username.value) return; // Can't chat with self
  
    selectedUser.value = user;
    currentChatMessages.value = messages[user.username] || [];
  
    // Derive shared key if not already done
    if (!sharedKeys[user.username]) {
        console.log(`Deriving shared key with ${user.username}...`);
        try {
            const importedPublicKey = await cryptoService.importPublicKey(user.publicKey);
            const derived = await cryptoService.deriveSharedKey(keyPair.privateKey, importedPublicKey);
            sharedKeys[user.username] = derived;
            console.log(`Shared key derived successfully with ${user.username}.`);
        } catch (error) {
            console.error(`Failed to derive shared key with ${user.username}:`, error);
            // Handle error - maybe disable sending messages to this user
        }
    }
      await nextTick(); // Ensure DOM updates before scrolling
      scrollToBottom();
  }
  
  async function sendMessage() {
    if (!newMessage.value.trim() || !selectedUser.value || !sharedKeys[selectedUser.value.username]) return;
  
    const recipientUsername = selectedUser.value.username;
    const sharedKey = sharedKeys[recipientUsername];
    const messageText = newMessage.value;
  
    try {
        // Encrypt the message
        const { iv, ciphertext } = await cryptoService.encryptData(sharedKey, messageText);
        const payload = JSON.stringify({ iv, ciphertext }); // Send IV and ciphertext as a JSON string
  
        // Send via SignalR
        await connection.invoke("SendMessage", recipientUsername, payload);
  
        // Add message to local chat history
        addMessageToChat(recipientUsername, {
            sender: username.value,
            text: messageText,
            timestamp: new Date()
        });
  
        newMessage.value = ''; // Clear input
        await nextTick();
        scrollToBottom(); // Scroll after adding message
  
    } catch (error) {
        console.error("Failed to encrypt or send message:", error);
        // Show error to user
    }
  }
  
  function addMessageToChat(chatPartnerUsername, message) {
      if (!messages[chatPartnerUsername]) {
          messages[chatPartnerUsername] = [];
      }
      messages[chatPartnerUsername].push(message);
  
      // If this chat is currently selected, update the displayed messages
      if (selectedUser.value?.username === chatPartnerUsername) {
          currentChatMessages.value = messages[chatPartnerUsername];
           nextTick(() => { // Ensure DOM is updated before scrolling
              scrollToBottom();
          });
      }
  }
  
   function scrollToBottom() {
      const container = messageContainer.value;
      if (container) {
          container.scrollTop = container.scrollHeight;
      }
  }
  
  // --- SignalR Event Listeners ---
  function setupSignalRListeners() {
    // Listen for updates to the user list from the server
    connection.on("UpdateUserList", (userList) => {
      console.log("Received user list:", userList);
      // Parse public keys back into JWK objects
      users.value = userList.map(u => ({
          ...u,
          publicKey: JSON.parse(u.publicKey) // Assuming server sends JWK as stringified JSON
      }));
    });
  
    // Listen for a new user connecting
    connection.on("UserConnected", (newUser) => {
      console.log("User connected:", newUser);
       // Avoid adding self if server echoes registration
       if(newUser.username === username.value) return;
  
       // Check if user already exists (e.g., due to reconnect)
       if (!users.value.some(u => u.username === newUser.username)) {
            users.value.push({
               ...newUser,
               publicKey: JSON.parse(newUser.publicKey)
           });
       }
    });
  
    // Listen for a user disconnecting
    connection.on("UserDisconnected", (disconnectedUsername) => {
      console.log("User disconnected:", disconnectedUsername);
      users.value = users.value.filter(u => u.username !== disconnectedUsername);
      if (selectedUser.value?.username === disconnectedUsername) {
        selectedUser.value = null; // Clear selection if the selected user left
        currentChatMessages.value = [];
      }
      // Clean up shared key for disconnected user (optional)
      delete sharedKeys[disconnectedUsername];
      delete messages[disconnectedUsername];
    });
  
    // Listen for incoming messages
    connection.on("ReceiveMessage", async (senderUsername, encryptedPayload) => {
      console.log(`Received encrypted message payload from ${senderUsername}`);
  
      // Derive shared key if this is the first message from this user
       if (!sharedKeys[senderUsername]) {
          console.log(`Deriving shared key with ${senderUsername} upon first message...`);
          const senderInfo = users.value.find(u => u.username === senderUsername);
          if(senderInfo && keyPair) {
               try {
                  const importedPublicKey = await cryptoService.importPublicKey(senderInfo.publicKey);
                  const derived = await cryptoService.deriveSharedKey(keyPair.privateKey, importedPublicKey);
                  sharedKeys[senderUsername] = derived;
                   console.log(`Shared key derived successfully with ${senderUsername}.`);
               } catch (error) {
                   console.error(`Failed to derive shared key with ${senderUsername}:`, error);
                   return; // Cannot decrypt without key
               }
          } else {
              console.error(`Cannot derive key for ${senderUsername}: User info or own key pair missing.`);
              return; // Cannot proceed
          }
       }
  
  
      const sharedKey = sharedKeys[senderUsername];
      if (!sharedKey) {
          console.error(`No shared key available for ${senderUsername}. Cannot decrypt.`);
          return;
      }
  
      try {
          const { iv, ciphertext } = JSON.parse(encryptedPayload); // Parse the received JSON string
          const decryptedText = await cryptoService.decryptData(sharedKey, iv, ciphertext);
  
          if (decryptedText !== null) {
               console.log(`Decrypted message from ${senderUsername}: ${decryptedText}`);
              addMessageToChat(senderUsername, {
                  sender: senderUsername,
                  text: decryptedText,
                  timestamp: new Date()
              });
          } else {
               // Decryption failed (integrity check likely failed or wrong key/IV)
               console.error(`Failed to decrypt message from ${senderUsername}. Possible tampering.`);
                addMessageToChat(senderUsername, {
                  sender: 'System',
                  text: `[Could not decrypt message from ${senderUsername}. Possible tampering or error.]`,
                  timestamp: new Date()
              });
          }
  
      } catch (error) {
          console.error(`Error processing received message from ${senderUsername}:`, error);
           addMessageToChat(senderUsername, {
                  sender: 'System',
                  text: `[Error processing message from ${senderUsername}.]`,
                  timestamp: new Date()
              });
      }
    });
  
     // Handle registration failure
      connection.on("RegistrationFailed", (reason) => {
          console.error("Registration failed:", reason);
          loginError.value = `Registration failed: ${reason}`;
          isLoggedIn.value = false; // Ensure user is marked as not logged in
          username.value = ''; // Clear username state
          connectionStatus.value = 'Registration Failed';
          // Optionally disconnect or prompt user to retry
          // stopConnection();
      });
  
     // Handle connection state changes (for status display)
     connection.onreconnecting((error) => {
          console.warn(`SignalR connection lost. Attempting to reconnect... Error: ${error}`);
          connectionStatus.value = 'Reconnecting...';
      });
  
      connection.onreconnected((connectionId) => {
          console.log(`SignalR reconnected with ID: ${connectionId}. Re-registering...`);
          connectionStatus.value = 'Reconnected. Re-registering...';
          // IMPORTANT: Need to re-register after reconnecting as the ConnectionId might change
          // and the server state for the old connection might be lost.
          // Reset state and trigger login logic again. This part needs careful handling
          // in a real app (e.g., store credentials securely). For this PoC, we might
          // just force the user back to the login screen upon reconnect.
          isLoggedIn.value = false; // Force re-login for simplicity in this PoC
          users.value = [];
          selectedUser.value = null;
          Object.keys(messages).forEach(key => delete messages[key]);
          currentChatMessages.value = [];
          Object.keys(sharedKeys).forEach(key => delete sharedKeys[key]); // Clear old keys
          connectionStatus.value = 'Reconnected. Please login again.';
          // You might automatically attempt re-login if you stored credentials,
          // but that adds complexity. Forcing re-login is safer for this scope.
  
          // If you *were* to re-register automatically:
          // if (username.value && keyPair) {
          //    login(); // Re-run the login process to register with the new connection ID
          // } else {
          //    // Fallback to manual login state
          // }
      });
  
      connection.onclose((error) => {
          console.error(`SignalR connection closed. Error: ${error}`);
          connectionStatus.value = 'Disconnected';
          // Consider informing the user or attempting to restart connection after a delay
           isLoggedIn.value = false; // Mark as logged out
           users.value = [];
           selectedUser.value = null;
           Object.keys(messages).forEach(key => delete messages[key]);
           currentChatMessages.value = [];
           Object.keys(sharedKeys).forEach(key => delete sharedKeys[key]);
      });
  }
  
  </script>
  
  <style scoped>
  .chat-container {
    display: flex;
    flex-direction: column;
    height: 80vh; /* Adjust height as needed */
    width: 600px; /* Adjust width */
    border: 1px solid #ccc;
    margin: 20px auto;
    font-family: sans-serif;
  }
  
   .chat-area {
      display: flex;
      flex-grow: 1; /* Takes remaining height */
      overflow: hidden; /* Prevent overall scrollbar */
   }
  
   .sidebar {
      width: 150px;
      border-right: 1px solid #ccc;
      padding: 10px;
      overflow-y: auto; /* Scroll if many users */
      flex-shrink: 0; /* Prevent shrinking */
   }
  
    .sidebar h3 {
       margin-top: 0;
       margin-bottom: 10px;
       font-size: 1em;
    }
    .sidebar ul {
      list-style: none;
      padding: 0;
      margin: 0;
    }
    .sidebar li {
      padding: 5px;
      cursor: pointer;
      border-radius: 3px;
    }
    .sidebar li:hover {
      background-color: #eee;
    }
    .sidebar li.selected {
      background-color: #ddd;
      font-weight: bold;
    }
  
  
   .main-chat {
      flex-grow: 1; /* Takes remaining width */
      display: flex;
      flex-direction: column;
      overflow: hidden; /* Contains messages and input */
   }
  
    .main-chat h3 {
       margin: 0;
       padding: 10px;
       border-bottom: 1px solid #ccc;
       background-color: #f8f8f8;
       font-size: 1.1em;
   }
  
  .messages {
    flex-grow: 1; /* Takes up available space */
    overflow-y: auto; /* Enable scrolling for messages */
    padding: 10px;
    background-color: #f9f9f9;
    border-bottom: 1px solid #ccc;
  }
  
  .message {
    margin-bottom: 10px;
    padding: 8px 12px;
    border-radius: 15px;
    max-width: 70%;
    word-wrap: break-word;
  }
  
  .message.sent {
    background-color: #dcf8c6;
    margin-left: auto; /* Align sent messages to the right */
    border-bottom-right-radius: 5px;
  }
  
  .message.received {
    background-color: #fff;
    border: 1px solid #eee;
    margin-right: auto; /* Align received messages to the left */
     border-bottom-left-radius: 5px;
  }
   .message strong {
      display: block;
      font-size: 0.8em;
      color: #555;
      margin-bottom: 3px;
   }
   .message .timestamp {
      display: block;
      font-size: 0.7em;
      color: #999;
      text-align: right;
      margin-top: 4px;
   }
  
  .message-input {
    display: flex;
    padding: 10px;
    border-top: 1px solid #ccc;
    background-color: #f0f0f0;
  }
  
  .message-input input {
    flex-grow: 1;
    padding: 8px;
    border: 1px solid #ccc;
    border-radius: 5px;
    margin-right: 5px;
  }
  
  .message-input button {
    padding: 8px 15px;
    border: none;
    background-color: #007bff;
    color: white;
    border-radius: 5px;
    cursor: pointer;
  }
   .message-input button:disabled {
      background-color: #aaa;
      cursor: not-allowed;
   }
   .message-input .warning {
       font-size: 0.8em;
       color: #e87b00;
       margin-left: 10px;
       align-self: center;
   }
  
   p {
      padding: 0 10px;
      font-size: 0.9em;
      color: #666;
   }
    .error {
      color: red;
      font-size: 0.9em;
      margin-top: 5px;
    }
  
  /* Login section styling */
  div[v-if="!isLoggedIn"] {
      padding: 20px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-direction: column;
       flex-grow: 1; /* Center login vertically */
  }
   div[v-if="!isLoggedIn"] input {
      padding: 10px;
      margin-bottom: 10px;
      width: 200px;
   }
   div[v-if="!isLoggedIn"] button {
      padding: 10px 20px;
   }
  </style>
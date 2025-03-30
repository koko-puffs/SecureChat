<template>
  <!-- Main container - Full height, dark background -->
  <div class="flex flex-col h-screen bg-neutral-800 text-neutral-100 font-sans">

    <!-- Login Section -->
    <div v-if="!isLoggedIn" class="flex-grow flex items-center justify-center p-5">
      <div class="w-full max-w-xs text-center">
        <h2 class="text-2xl font-semibold mb-6 text-white">Secure Chat</h2>
        <input
          type="text"
          v-model="usernameInput"
          placeholder="Enter your username"
          @keyup.enter="login"
          class="w-full p-3 mb-4 bg-neutral-700 border border-neutral-600 rounded-lg text-white placeholder-neutral-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition"
        />
        <button
          @click="login"
          :disabled="!usernameInput.trim()"
          class="w-full p-3 bg-blue-600 text-white font-semibold rounded-lg hover:bg-blue-700 disabled:bg-neutral-600 disabled:opacity-75 disabled:cursor-not-allowed transition duration-150 ease-in-out"
        >
          Connect
        </button>
        <p v-if="loginError" class="text-red-400 text-sm mt-3">{{ loginError }}</p>
      </div>
    </div>

    <!-- Chat Section -->
    <div v-if="isLoggedIn" class="flex flex-grow overflow-hidden">

      <!-- Sidebar: User List -->
      <div class="w-64 bg-neutral-900 p-4 flex flex-col flex-shrink-0 border-r border-neutral-700">
        <h3 class="text-lg font-semibold mb-4 text-neutral-200">Online Users</h3>
        <div class="overflow-y-auto flex-grow -mr-4 pr-4">
           <!-- Add negative margin and padding to hide scrollbar visually but keep functionality -->
          <ul v-if="users.length > 0" class="space-y-1">
             <!-- Only show list if more than just 'You' are potentially online -->
            <li
              v-for="user in users"
              :key="user.username"
              @click="selectUser(user)"
              :class="[
                'p-2.5 rounded-lg cursor-pointer transition-colors duration-150 ease-in-out text-neutral-300 flex items-center space-x-2',
                selectedUser?.username === user.username
                  ? 'bg-neutral-700 font-medium text-white'
                  : 'hover:bg-neutral-700/50',
                user.username === username ? 'opacity-75 italic' : '' // Style 'You' subtly
              ]"
            >
             <!-- Basic status indicator (can be enhanced) -->
             <span class="h-2 w-2 bg-green-500 rounded-full flex-shrink-0"></span>
             <span class="truncate">{{ user.username }} {{ user.username === username ? '(You)' : '' }}</span>
            </li>
          </ul>
          <p v-else-if="users.length === 1 && users[0].username === username" class="text-sm text-neutral-500 px-2">Waiting for others...</p>
          <p v-else class="text-sm text-neutral-500 px-2">No users online.</p>
        </div>
        <!-- Logout button or user info can go here -->
        <div class="mt-auto pt-4 border-neutral-700">
           <p class="text-xs text-neutral-500">{{ connectionStatus }}</p>
        </div>
      </div>

      <!-- Main Chat Area -->
      <div class="flex-grow flex flex-col bg-neutral-800">
        <!-- Chat Header -->
        <div v-if="selectedUser" class="p-4 border-b border-neutral-700 bg-neutral-800 flex items-center space-x-3">
            <!-- Placeholder for Avatar -->
           <div class="w-8 h-8 rounded-full bg-neutral-600 flex items-center justify-center font-semibold text-sm flex-shrink-0">
               {{ selectedUser.username.substring(0, 1).toUpperCase() }}
           </div>
           <h3 class="font-semibold text-lg text-neutral-200 truncate">
            Chatting with {{ selectedUser.username }}
          </h3>
           <!-- Key Status Indicator -->
           <span v-if="!sharedKeys[selectedUser.username]" class="text-xs text-yellow-400 ml-auto animate-pulse">(Establishing secure connection...)</span>
           <span v-else class="text-xs text-green-400 ml-auto">(Secure)</span>
        </div>

        <!-- Message Display Area -->
        <div v-if="selectedUser" class="flex-grow overflow-y-auto p-4 lg:p-6 space-y-4 max-w-4xl mx-auto w-full" ref="messageContainer">
          <div
            v-for="(msg, index) in currentChatMessages"
            :key="index"
            :class="[
              'flex w-full',
              msg.sender === username ? 'justify-end' : 'justify-start'
            ]"
          >
            <div
              :class="[
                'p-3 px-4 rounded-xl max-w-[85%] break-words shadow-md',
                 msg.sender === username
                  ? 'bg-blue-600 text-white rounded-br-lg' // Changed sender bubble style
                  : 'bg-neutral-700 text-neutral-100 rounded-bl-lg' // Changed receiver bubble style
              ]"
            >
               <!-- Removed sender name from bubble for cleaner look -->
              <p class="text-sm">{{ msg.text }}</p>
              <span class="block text-xs text-right mt-1.5 opacity-60">
                {{ msg.timestamp.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) }}
              </span>
            </div>
          </div>
          <!-- System message for decryption errors -->
           <div v-if="currentChatMessages.some(m => m.sender === 'System')" class="text-center text-xs text-red-400 py-2">
              Some messages may not be displayed correctly due to errors.
           </div>
        </div>

        <!-- Placeholder when no user is selected -->
        <div v-else class="flex-grow flex items-center justify-center">
          <p class="text-neutral-500 text-lg">Select a user to start chatting</p>
        </div>

        <!-- Message Input Area -->
        <div class="p-4 border-t border-neutral-700 bg-neutral-800">
           <!-- Key Exchange Pending Message -->
           <p
              v-if="selectedUser && !sharedKeys[selectedUser.username]"
              class="text-xs text-yellow-400 mb-2 text-center"
            >
              Please wait while a secure connection with {{selectedUser.username}} is established... Input disabled.
            </p>
          <div class="flex items-center space-x-3 max-w-4xl mx-auto">
            <input
              type="text"
              v-model="newMessage"
              placeholder="Type your message here..."
              @keyup.enter="sendMessage"
              :disabled="!selectedUser || !sharedKeys[selectedUser.username]"
              class="flex-grow p-3 bg-neutral-700 border border-neutral-600 rounded-lg text-white placeholder-neutral-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition disabled:opacity-50"
            />
            <button
              @click="sendMessage"
              :disabled="!newMessage.trim() || !selectedUser || !sharedKeys[selectedUser.username]"
              class="p-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:bg-neutral-600 disabled:opacity-75 disabled:cursor-not-allowed transition duration-150 ease-in-out flex-shrink-0 px-5 font-semibold"
            >
              Send
            </button>
          </div>
        </div>
      </div>
    </div>

  </div>
</template>

<script lang="ts" setup>
import { ref, reactive, onMounted, onUnmounted, nextTick } from 'vue';
import { createConnection, getConnection, startConnection, stopConnection } from '../services/signalr';
import * as cryptoService from '../services/crypto';

interface User {
  username: string;
  publicKey: JsonWebKey;
}

interface Message {
  sender: string;
  text: string;
  timestamp: Date;
}

interface EncryptedData {
  iv: string;
  ciphertext: string;
}

const username = ref('');
const usernameInput = ref('');
const isLoggedIn = ref(false);
const loginError = ref('');
const connectionStatus = ref('Disconnected');
const users = ref<User[]>([]); 
const selectedUser = ref<User | null>(null);
const messages = reactive<Record<string, Message[]>>({});
const currentChatMessages = ref<Message[]>([]);
const newMessage = ref('');
const messageContainer = ref<HTMLElement | null>(null);

let connection: any = null;
let keyPair: { publicKey: CryptoKey, privateKey: CryptoKey } | null = null;
const sharedKeys = reactive<Record<string, CryptoKey>>({});

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

  } catch (error: any) {
    console.error("Login failed:", error);
    loginError.value = `Login failed: ${error.message || 'Unknown error'}`;
    isLoggedIn.value = false; // Revert optimistic login
    connectionStatus.value = 'Login Failed';
  }
}

async function selectUser(user: User) {
  if (user.username === username.value) return; // Can't chat with self

  selectedUser.value = user;
  currentChatMessages.value = messages[user.username] || [];

  // Derive shared key if not already done
  if (!sharedKeys[user.username]) {
    console.log(`Deriving shared key with ${user.username}...`);
    try {
      const importedPublicKey = await cryptoService.importPublicKey(user.publicKey);
      if (keyPair) {
        const derived = await cryptoService.deriveSharedKey(keyPair.privateKey, importedPublicKey);
        sharedKeys[user.username] = derived;
        console.log(`Shared key derived successfully with ${user.username}.`);
      }
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

function addMessageToChat(chatPartnerUsername: string, message: Message) {
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
  connection.on("UpdateUserList", (userList: any[]) => {
    console.log("Received user list:", userList);
    // Parse public keys back into JWK objects
    users.value = userList.map(u => ({
      ...u,
      publicKey: JSON.parse(u.publicKey) // Assuming server sends JWK as stringified JSON
    }));
  });

  // Listen for a new user connecting
  connection.on("UserConnected", (newUser: any) => {
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
  connection.on("UserDisconnected", (disconnectedUsername: string) => {
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
  connection.on("ReceiveMessage", async (senderUsername: string, encryptedPayload: string) => {
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
      const { iv, ciphertext } = JSON.parse(encryptedPayload) as EncryptedData;
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
  connection.on("RegistrationFailed", (reason: string) => {
    console.error("Registration failed:", reason);
    loginError.value = `Registration failed: ${reason}`;
    isLoggedIn.value = false; // Ensure user is marked as not logged in
    username.value = ''; // Clear username state
    connectionStatus.value = 'Registration Failed';
    // Optionally disconnect or prompt user to retry
    // stopConnection();
  });

  // Handle connection state changes (for status display)
  connection.onreconnecting((error: Error) => {
    console.warn(`SignalR connection lost. Attempting to reconnect... Error: ${error}`);
    connectionStatus.value = 'Reconnecting...';
  });

  connection.onreconnected((connectionId: string) => {
    console.log(`SignalR reconnected with ID: ${connectionId}. Re-registering...`);
    connectionStatus.value = 'Reconnected. Re-registering...';
    // IMPORTANT: Need to re-register after reconnecting as the ConnectionId might change
    // and the server state for the old connection might be lost.
    // Reset state and trigger login logic again.
    isLoggedIn.value = false; // Force re-login for simplicity in this PoC
    users.value = [];
    selectedUser.value = null;
    Object.keys(messages).forEach(key => delete messages[key]);
    currentChatMessages.value = [];
    Object.keys(sharedKeys).forEach(key => delete sharedKeys[key]); // Clear old keys
    connectionStatus.value = 'Reconnected. Please login again.';
  });

  connection.onclose((error?: Error) => {
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
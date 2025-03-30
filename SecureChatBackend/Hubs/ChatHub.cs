using Microsoft.AspNetCore.SignalR;
using SecureChatBackend.Models;
using System.Collections.Concurrent; // Thread-safe dictionary

namespace SecureChatBackend.Hubs;

public class ChatHub : Hub
{
    // Use ConcurrentDictionary for thread safety with SignalR
    private static readonly ConcurrentDictionary<string, UserInfo> Users = new(); // Key: ConnectionId

    // Called when a client connects
    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"Client connected: {Context.ConnectionId}");
        await base.OnConnectedAsync();
        // We wait for the client to register with a username and public key
    }

    // Called when a client disconnects
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Console.WriteLine($"Client disconnected: {Context.ConnectionId}");
        if (Users.TryRemove(Context.ConnectionId, out UserInfo? userInfo))
        {
            Console.WriteLine($"User {userInfo.Username} removed.");
            // Notify other users that this user has left
            await Clients.Others.SendAsync("UserDisconnected", userInfo.Username);
        }
        await base.OnDisconnectedAsync(exception);
    }

    // Client calls this after connecting to identify itself
    public async Task Register(string username, string publicKeyJwk)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(publicKeyJwk))
        {
            // Maybe send an error back to the client
            Console.WriteLine($"Registration failed for {Context.ConnectionId}: Missing username or public key.");
            return;
        }

         // Check if username is already taken (simple check)
        if (Users.Any(u => u.Value.Username.Equals(username, StringComparison.OrdinalIgnoreCase))) {
             await Clients.Caller.SendAsync("RegistrationFailed", "Username already taken.");
             Context.Abort(); // Disconnect client if username taken
             return;
        }


        var newUserInfo = new UserInfo
        {
            ConnectionId = Context.ConnectionId,
            Username = username,
            PublicKey = publicKeyJwk // Store the public key received from the client
        };

        if (Users.TryAdd(Context.ConnectionId, newUserInfo))
        {
            Console.WriteLine($"User {username} registered with ConnectionId {Context.ConnectionId}");

            // Send the current list of users (excluding self) to the newly registered client
            var otherUsers = Users.Values
                                  .Where(u => u.ConnectionId != Context.ConnectionId)
                                  .Select(u => new { u.Username, u.PublicKey })
                                  .ToList();
            await Clients.Caller.SendAsync("UpdateUserList", otherUsers);

            // Notify all other clients about the new user
            await Clients.Others.SendAsync("UserConnected", new { username = newUserInfo.Username, publicKey = newUserInfo.PublicKey });
        } else {
             await Clients.Caller.SendAsync("RegistrationFailed", "Failed to register user on server.");
             Context.Abort();
        }
    }

    // Client calls this to send an encrypted message to another user
    public async Task SendMessage(string toUsername, string encryptedMessagePayload) // Payload contains iv and ciphertext
    {
         if (!Users.TryGetValue(Context.ConnectionId, out UserInfo? senderInfo))
        {
            Console.WriteLine($"Error: Sender {Context.ConnectionId} not found.");
            return; // Or send error to caller
        }

        // Find the recipient's connection ID
        var recipient = Users.Values.FirstOrDefault(u => u.Username.Equals(toUsername, StringComparison.OrdinalIgnoreCase));

        if (recipient != null)
        {
            Console.WriteLine($"Relaying message from {senderInfo.Username} to {toUsername}");
            // Send the encrypted payload to the specific recipient client
            // Include the sender's username so the recipient knows who it's from
            await Clients.Client(recipient.ConnectionId).SendAsync("ReceiveMessage", senderInfo.Username, encryptedMessagePayload);
        }
        else
        {
            Console.WriteLine($"Error: Recipient {toUsername} not found or not connected.");
            // Optionally notify the sender that the recipient is offline
            // await Clients.Caller.SendAsync("Error", $"User {toUsername} is not online.");
        }
    }
}
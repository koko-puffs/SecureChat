import * as signalR from "@microsoft/signalr";

const HUB_URL = "http://localhost:5087/chathub"; // Adjust port if backend runs elsewhere, use https if needed

let connection = null;

export function createConnection() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl(HUB_URL)
        .withAutomaticReconnect() // Optional: handle reconnections automatically
        .configureLogging(signalR.LogLevel.Information) // Optional: logging
        .build();

    return connection;
}

export function getConnection() {
    if (!connection) {
        throw new Error("SignalR connection has not been created.");
    }
    return connection;
}

export async function startConnection() {
    if (connection && connection.state === signalR.HubConnectionState.Disconnected) {
        try {
            await connection.start();
            console.log("SignalR Connected.");
        } catch (err) {
            console.error("SignalR Connection Error: ", err);
            // Implement retry logic or user notification here
            setTimeout(startConnection, 5000); // Simple retry
        }
    }
}

export async function stopConnection() {
     if (connection && connection.state === signalR.HubConnectionState.Connected) {
         await connection.stop();
         console.log("SignalR Disconnected.");
     }
}
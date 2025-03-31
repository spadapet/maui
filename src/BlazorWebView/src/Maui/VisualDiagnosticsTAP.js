const tap_webSocket = new WebSocket('ws://localhost:5000/ws', "ms-vdep");
let tap_isConnected = false;
const tap_messageQueue = [];

function sendMessage(message) {
    console.log(`*** TAP: Sending message: ${message}`);
    if (tap_isConnected && tap_webSocket) {
        tap_webSocket.send(message);
    } else {
        console.warn('WebSocket is not connected or is null. Queuing message.');
        tap_messageQueue.push(message);
    }
}

// Add a DOM mutation observer
const tap_observer = new MutationObserver((mutationsList) => {
    for (const mutation of mutationsList) {
        const message = JSON.stringify({
            type: 'mutation',
            mutation: {
                type: mutation.type,
                target: mutation.target.tagName,
                addedNodes: Array.from(mutation.addedNodes).map(node => node.tagName),
                removedNodes: Array.from(mutation.removedNodes).map(node => node.tagName),
                attributeName: mutation.attributeName,
                oldValue: mutation.oldValue
            }
        });
        sendMessage(message);
    }
});

tap_observer.observe(document, {
    attributes: true,
    childList: true,
    subtree: true,
    characterData: true
});

tap_webSocket.onopen = () => {
    console.log('*** TAP: Connected to WebSocket server');
    tap_isConnected = true;

    alert('Connected to WebSocket server');

    // Process any queued messages
    while (tap_messageQueue.length > 0) {
        const message = tap_messageQueue.shift();
        tap_webSocket.send(message);
        console.log(`*** TAP: Sent queued message: ${message}`);
    }
};

tap_webSocket.onmessage = (event) => {
    console.log(`*** TAP: Received message: ${event.data}`);
};

tap_webSocket.onclose = () => {
    console.log('*** TAP: WebSocket connection closed');

    alert('Closed WebSocket connection');

    tap_isConnected = false;
    tap_observer.disconnect();
};

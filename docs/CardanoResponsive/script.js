// Responsive Mobile Check
if (/iPhone|iPad|iPod|Android/i.test(navigator.userAgent)) {
    var meta = document.createElement('meta');
    meta.name = 'viewport';
    meta.content = 'width=device-width, height=device-height, initial-scale=1.0, user-scalable=no, shrink-to-fit=yes';
    document.getElementsByTagName('head')[0].appendChild(meta);
}

// Function called by HTML Button
function toggleWallet() {
    // Check if the Unity instance has loaded from the main HTML file
    if (typeof myGameInstance === 'undefined' || !myGameInstance) {
        console.log("Unity not loaded yet");
        return;
    }
    
    // Update button text to give user feedback
    document.getElementById("connect-btn").innerText = "Connecting...";
    
    // Trigger the connection loop 
    myGameInstance.SendMessage("CardanoManager", "RequestConnection");
}

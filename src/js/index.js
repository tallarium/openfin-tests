document.addEventListener("DOMContentLoaded", function(){
    init();
});

function init(){
    if (window.fin) {
        initWithOpenFin();
    } else if (window.__TAURI__) {
        initWithTauri();
    } else {
        initNoContainer();
    }
}

function initWithOpenFin(){
    console.log("OpenFin is available");
    // Your OpenFin specific code to go here...
}

function initWithTauri(){
    console.log("Tauri is available");
    // Your Tauri specific code to go here...
}

function initNoContainer(){
    alert("No container available - you are probably running in a browser.");
    // Your browser-only specific code to go here...
}
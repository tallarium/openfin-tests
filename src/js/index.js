document.addEventListener("DOMContentLoaded", function(){
    init();
});

function init(){
    /* Code common to both OpenFin and browser to go above.
     Then the specific code for OpenFin and browser only to be
     targeted in the try/catch block below.
     */
    try{
        fin.desktop.main(function(){
            initWithOpenFin();
        })
    }catch(err){
        initNoOpenFin();
    }
}

function runMyAsset() {
    return new Promise((resolve, reject) => {
        fin.desktop.System.launchExternalProcess({
            alias: "myAsset",
            listener: function (result) {
                console.log('the exit code', result.exitCode);
                resolve();
            }
        }, function (payload) {
            console.log('Success:', payload.uuid);
        }, function (error) {
            console.log('Error:', error);
            reject(error);
        });
        blockCpu();
    })
}

function blockCpu() {
    console.log("blocked");
    const start =new Date();
    let result =0;
    do {
        result +=Math.random() *Math.random();
    }
    while(new Date() -start < 2000);
    console.log("unblocked");
    return result;
}

async function initWithOpenFin(){
    await runMyAsset();
    alert('Done');
}

function initNoOpenFin(){
    alert("OpenFin is not available - you are probably running in a browser.");
    // Your browser-only specific code to go here...
}

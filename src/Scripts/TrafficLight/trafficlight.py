import http.server
import socketserver
import threading
import time


mode = "color" # color/image
timeDelay = 3 # amount of seconds for the green signal
port_80 = 8000

html_page = """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Traffic Light</title>
</head>
<body bgcolor="black">
    <div id="content"></div>

    <script>
        // Function to fetch and update data from the /status endpoint
        function fetchData() {
            fetch('http://localhost:8000/status') // Replace with your actual endpoint URL
                .then(response => response.text())
                .then(data => {
                    // Update the content of the 'data' div with the fetched data
                    document.getElementById('content').textContent = data;
                    if(data == 'green')
                    {
                        document.body.style.backgroundColor = 'green'
                    }
                    else
                    {
                        document.body.style.backgroundColor = 'red'
                    }
                })
                .catch(error => console.error('Error:', error));
        }

        // Periodically fetch data (e.g., every 5 seconds)
        setInterval(fetchData, 100); // Adjust the interval as needed (in milliseconds)
        
        // Initial data fetch
        fetchData();
    </script>
</html>
"""

status = "red"
startTimer : time

class MyHandler(http.server.SimpleHTTPRequestHandler):
    def do_GET(self):
        global status
        global startTimer
        global timeDelay
        global html_page
               
        if self.path == '/go':
            print("go")
            status = "green" 
            startTimer = time.time()
            
            self.send_response(200)
            self.send_header('Content-type', 'text/html')
            self.send_header('Access-Control-Allow-Origin', '*')  # Allow requests from any origin
            self.send_header('Access-Control-Allow-Methods', 'GET')  # Allow GET requests
            self.send_header('Access-Control-Allow-Headers', 'Content-Type')  # Allow Content-Type header
            self.end_headers()
            messageToBeSent = "GO! signal for the next 3 seconds."
            self.wfile.write(messageToBeSent.encode())
            
        elif self.path == '/status':
            
            currentTimer = time.time()
            
            if(status == "green"):
                diffTimer = currentTimer - startTimer
                print("diffTimer:" +str(diffTimer))
                
                if(diffTimer > timeDelay):
                    status = "red"
                    diffTimer = 0
            
            print("status: " + status)
            
            self.send_response(200)
            self.send_header('Content-type', 'text/html')
            self.send_header('Access-Control-Allow-Origin', '*')  # Allow requests from any origin
            self.send_header('Access-Control-Allow-Methods', 'GET')  # Allow GET requests
            self.send_header('Access-Control-Allow-Headers', 'Content-Type')  # Allow Content-Type header
            self.end_headers()
            self.wfile.write(status.encode())

        else:
            self.send_response(200)
            self.send_header('Content-type', 'text/html')
            self.send_header('Access-Control-Allow-Origin', '*')  # Allow requests from any origin
            self.send_header('Access-Control-Allow-Methods', 'GET')  # Allow GET requests
            self.send_header('Access-Control-Allow-Headers', 'Content-Type')  # Allow Content-Type header
            self.end_headers()
            self.wfile.write(html_page.encode())
        

# Create server instance for defined port
server_80 = socketserver.TCPServer(("127.0.0.1", port_80), MyHandler)

print(f"Serving on 127.0.0.1:{port_80}")

# Start each server in a separate thread
server_thread_80 = threading.Thread(target=server_80.serve_forever)
server_thread_80.daemon = True
server_thread_80.start()

try:
    while True:
        pass  # Keep the script running to handle requests

except KeyboardInterrupt:
    print("\nServers interrupted by the user.")

finally:
    # Shutdown the servers gracefully
    server_80.shutdown()
    server_80.server_close()

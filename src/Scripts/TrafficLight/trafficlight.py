import http.server
import socketserver
import threading
import time
import json


# Open the JSON file for reading
try:
    with open('config.json', 'r') as json_file:
        # Load the JSON data from the file
        data = json.load(json_file)

except FileNotFoundError:
    print("Warning: The 'data.json' file was not found. Exiting... (Press any key to exit)")
    input()
    exit()

# Assign data from JSON to variables
timeDelay = data['timeDelay']
binding_ip = data['binding_ip']
binding_port = data['binding_port']
checkInterval = data['checkInterval']

print("Traffic Light")
print()
print("Configuration:")
print("Time Delay: " + str(timeDelay))
print("Binding Ip: " + binding_ip)
print("Binding Port: " + str(binding_port))
print("Check Interval: " + str(checkInterval))



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
        function fetchData() {{
            fetch('http://{}:{}' + '/status') // Replace with your actual endpoint URL
                .then(response => response.text())
                .then(data => {{
                    // Update the content of the 'data' div with the fetched data
                    document.getElementById('content').textContent = data;
                    if(data == 'green')
                    {{
                        document.body.style.backgroundColor = 'green'
                    }}
                    else
                    {{
                        document.body.style.backgroundColor = 'red'
                    }}
                }})
                .catch(error => console.error('Error:', error));
        }}

        // Periodically fetch data
        setInterval(fetchData, {}); // Adjust the interval as needed (in milliseconds)
        
        // Initial data fetch
        fetchData();
    </script>
</html>
""".format(binding_ip, binding_port, checkInterval)

status = "red"
startTimer : time

class MyHandler(http.server.SimpleHTTPRequestHandler):
    def do_GET(self):
        global status
        global startTimer
        global timeDelay
        global html_page
               
        if self.path == '/go':
            print("go GET")
            status = "green" 
            startTimer = time.time()
            
            self.send_response(200)
            self.send_header('Content-type', 'text/html')
            self.send_header('Access-Control-Allow-Origin', '*')  # Allow requests from any origin
            self.send_header('Access-Control-Allow-Methods', 'GET')  # Allow GET requests
            self.send_header('Access-Control-Allow-Headers', 'Content-Type')  # Allow Content-Type header
            self.end_headers()
            messageToBeSent = "GO! signal for the next {timeDelay} seconds."
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
    
    def do_POST(self):
        global status
        global startTimer
        global timeDelay
        global html_page
               
        if self.path == '/go':
            print("go POST")
            status = "green" 
            startTimer = time.time()
            
            self.send_response(200)
            self.send_header('Content-type', 'text/html')
            self.send_header('Access-Control-Allow-Origin', '*')  # Allow requests from any origin
            self.send_header('Access-Control-Allow-Methods', 'GET')  # Allow GET requests
            self.send_header('Access-Control-Allow-Headers', 'Content-Type')  # Allow Content-Type header
            self.end_headers()
            messageToBeSent = "GO! signal for the next {timeDelay} seconds."
            self.wfile.write(messageToBeSent.encode())
            

# Create server instance for defined port
server_80 = socketserver.TCPServer((binding_ip, binding_port), MyHandler)

print(f"Serving on {binding_ip}:{binding_port}")

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
import http.server
import socketserver
import threading
import time

# Define the delay before go and stop messages
timeDelay = 3
webRefreshTime = 1
setupURL = "http://127.0.0.1"
webTitle = "Traffic Light"
webPort = "8000"
greenPort = "8001"
redColor = "red"
redMessage = "Please dont go"
greenColor = "green"
greenMessage = "You Can go now"

# Define the response messages
message_red = f"<html><head><meta http-equiv='refresh' content='{webRefreshTime};url={setupURL}:{webPort}'><title>{webTitle}</title><head><body bgcolor='{redColor}'><center><font size='20' color='white' weight='900'>{redMessage}</font></center></body></html>"
messageGreen = f"<html><head><meta http-equiv='refresh' content='{webRefreshTime};url={setupURL}:{webPort}'><title>{webTitle}</title><head><body bgcolor='{greenColor}'><center><font size='20' color='white' weight='900'>{greenMessage}</font></center></body></html>"

currentStep = 0
startTimer : time
currentStep = 0

# Define the custom request handler
class MyHandler(http.server.SimpleHTTPRequestHandler):
    def do_GET(self):
        global shared_message
        global currentStep
        global startTimer
        
        server_port = self.server.socket.getsockname()[1]  # Get the server's port
        
        if server_port == 8001:
            currentStep = 1
            startTimer = time.time()
        else:
            currentTimer = time.time()
            
            if(currentStep == 1):
                diffTimer = currentTimer - startTimer
                print("diffTimer:" +str(diffTimer))
            else:
                diffTimer = 0
            
            if(diffTimer > timeDelay):
                currentStep = 0
                diffTimer = 0
        
            self.send_response(200)
            self.send_header('Content-type', 'text/html')
            self.end_headers()
            
            if(currentStep == 1):
                self.wfile.write(messageGreen.encode())
            else:
                self.wfile.write(message_red.encode())

# Create server instances for ports 8000 and 8001
port_8000 = 8000
port_8001 = 8001

server_8000 = socketserver.TCPServer(("127.0.0.1", port_8000), MyHandler)
server_8001 = socketserver.TCPServer(("127.0.0.1", port_8001), MyHandler)

print(f"Serving on 127.0.0.1:{port_8000} (Initial Response)")
print(f"Serving on 127.0.0.1:{port_8001} (Updater)")

# Start each server in a separate thread
server_thread_8000 = threading.Thread(target=server_8000.serve_forever)
server_thread_8001 = threading.Thread(target=server_8001.serve_forever)

server_thread_8000.daemon = True
server_thread_8001.daemon = True

server_thread_8000.start()
server_thread_8001.start()

try:
    while True:
        # Listen for user input to update the shared message
        user_input = input("press any key and press Enter to exit")
        if user_input:
            server_8000.shutdown()
            server_8000.server_close()
            server_8001.shutdown()
            server_8001.server_close()
            exit()
except KeyboardInterrupt:
    print("\nServers interrupted by the user.")

finally:
    # Shutdown the servers gracefully
    server_8000.shutdown()
    server_8000.server_close()
    server_8001.shutdown()
    server_8001.server_close()

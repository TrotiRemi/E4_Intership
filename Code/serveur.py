import http.server
import socketserver
import json
import os
from datetime import datetime

PORT = 8888
DATA_FILE = "received_data.json"

class Handler(http.server.BaseHTTPRequestHandler):
    def do_POST(self):
        content_length = int(self.headers.get('Content-Length', 0))
        post_data = self.rfile.read(content_length).decode('utf-8')
        print("Reçu :", post_data)
        if "text/csv" in self.headers.get('Content-Type', ''):
            with open("qoe_data_received.csv", "wb") as f:
                f.write(post_data.encode("utf-8"))
            print("CSV reçu et sauvegardé.")

        file_path = os.path.join(os.path.dirname(__file__), DATA_FILE)

        try:
            with open(file_path, "r", encoding="utf-8") as f:
                data_list = json.load(f)
        except (FileNotFoundError, json.JSONDecodeError):
            data_list = []

        data_list.append({
            "timestamp": datetime.now().isoformat(),
            "content": post_data
        })

        with open(file_path, "w", encoding="utf-8") as f:
            json.dump(data_list, f, ensure_ascii=False, indent=4)

        self.send_response(200)
        self.end_headers()

print(f"Serveur HTTP démarré sur http://localhost:{PORT}")
with socketserver.TCPServer(("", PORT), Handler) as httpd:
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\nArrêt du serveur...")
    finally:
        httpd.server_close()

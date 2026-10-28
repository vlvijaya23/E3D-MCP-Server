import requests

print(
    requests.get(
        "http://localhost:8765/current"
    ).text
)
import json
import requests

with open("model.json", "r") as f:
    model = json.load(f)

members = model["members"]

print(f"Members Loaded: {len(members)}")

for m in members[:10]:

    member_id = m["member"]
    member_type = m["type"]

    if member_type == "COLUMN":

        r = requests.get(
            f"http://localhost:8765/createcolumn?name=C{member_id}"
        )

    else:

        r = requests.get(
            f"http://localhost:8765/createbeam?name=B{member_id}"
        )

    print(r.text)
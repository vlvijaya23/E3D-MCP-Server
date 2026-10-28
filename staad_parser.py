import re
import json


def parse_nodes(std_file):
    nodes = []

    with open(std_file, "r") as f:
        lines = f.readlines()

    capture = False

    for line in lines:

        line = line.strip()

        if "JOINT COORDINATES" in line.upper():
            capture = True
            continue

        if capture:

            # Stop when another STAAD section starts
            if (
                "MEMBER INCIDENCES" in line.upper()
                or "START GROUP" in line.upper()
                or "MEMBER PROPERTY" in line.upper()
            ):
                break

            if not line:
                continue

            nums = re.findall(r"[-+]?\d*\.?\d+", line)

            # Need node id + X + Y + Z
            if len(nums) < 4:
                continue

            try:
                node_id = int(float(nums[0]))

                x = float(nums[1])
                y = float(nums[2])
                z = float(nums[3])

                nodes.append(
                    {
                        "id": node_id,
                        "x": x,
                        "y": y,
                        "z": z
                    }
                )

            except Exception:
                continue

    return nodes

def parse_members(std_file):
    members = []

    with open(std_file, "r") as f:
        lines = f.readlines()

    capture = False

    for line in lines:

        line = line.strip()

        if "MEMBER INCIDENCES" in line.upper():
            capture = True
            continue

        if capture:

            if "START GROUP DEFINITION" in line.upper():
                break

            parts = line.split(";")

            for part in parts:

                part = part.strip()

                if not part:
                    continue

                nums = re.findall(r"\d+", part)

                if len(nums) >= 3:

                    members.append({
                        "member": int(nums[0]),
                        "start": int(nums[1]),
                        "end": int(nums[2])
                    })

    return members

def parse_columns(std_file):

    columns = []

    with open(std_file, "r") as f:
        lines = f.readlines()

    for line in lines:

        if line.strip().startswith("_COLUMN"):

            tokens = line.replace("_COLUMN", "").split()

            i = 0

            while i < len(tokens):

                if (
                    i + 2 < len(tokens)
                    and tokens[i + 1].upper() == "TO"
                ):
                    start = int(tokens[i])
                    end = int(tokens[i + 2])

                    for m in range(start, end + 1):
                        columns.append(m)

                    i += 3

                else:
                    try:
                        columns.append(int(tokens[i]))
                    except:
                        pass

                    i += 1

    return sorted(set(columns))

def export_json(nodes, members, columns):

    column_set = set(columns)

    enriched_members = []

    for m in members:

        member_type = "BEAM"

        if m["member"] in column_set:
            member_type = "COLUMN"

        enriched_members.append({
            "member": m["member"],
            "start": m["start"],
            "end": m["end"],
            "type": member_type
        })

    data = {
        "nodes": nodes,
        "members": enriched_members
    }

    with open("model.json", "w") as f:
        json.dump(data, f, indent=4)

    print("\nJSON Exported: model.json")

if __name__ == "__main__":

    nodes = parse_nodes("sample.STD")
    members = parse_members("sample.STD")
    columns = parse_columns("sample.STD")

    print(f"\nNodes Found: {len(nodes)}")
    print(f"Members Found: {len(members)}\n")

    print("First 10 Members\n")

    for m in members[:10]:
        print(m)

    print(f"\nColumns Found: {len(columns)}\n")

    print("First 20 Columns\n")

    for c in columns[:20]:
        print(c)

    export_json(nodes, members, columns)
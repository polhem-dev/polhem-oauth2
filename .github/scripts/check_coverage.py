"""Checks the line coverage of the packages in the Cobertura reports under a folder.

Usage: python3 check_coverage.py <folder with *.cobertura.xml>

Each package has a floor about two points below the coverage it had when the floor was set, so a change that drops a
package below it fails the build. Raise a floor when coverage rises; lower one only with a reason in the commit message.
The System.Web package runs its tests on Windows only and is not measured here.
"""

import os
import sys
import xml.etree.ElementTree as ElementTree

FLOORS = {
    "Polhem.OAuth2": 0.95,
    "Polhem.OAuth2.AspNetCore": 0.93,
}


def main(folder: str) -> int:
    reports = [
        os.path.join(root, name)
        for root, _, names in os.walk(folder)
        for name in names
        if name.endswith(".cobertura.xml")
    ]
    if not reports:
        print(f"No Cobertura report was found under {folder}.")
        return 1

    rates = {}
    for report in reports:
        for package in ElementTree.parse(report).getroot().iter("package"):
            rates[package.get("name")] = (float(package.get("line-rate")), float(package.get("branch-rate")))

    lines = ["| Package | Lines | Branches | Floor for lines |", "|---------|------:|---------:|----------------:|"]
    failed = False
    for name, floor in FLOORS.items():
        if name not in rates:
            print(f"The report has no coverage for {name}.")
            return 1
        line_rate, branch_rate = rates[name]
        below = line_rate < floor
        failed |= below
        lines.append(f"| {name} | {line_rate:.1%} | {branch_rate:.1%} | {floor:.0%}{' (below)' if below else ''} |")

    summary = "\n".join(["## Code coverage", "", *lines, ""])
    print(summary)
    if "GITHUB_STEP_SUMMARY" in os.environ:
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as output:
            output.write(summary)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))

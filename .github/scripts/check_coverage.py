"""Checks the line and branch coverage of the packages in the Cobertura reports under a folder.

Usage: python3 check_coverage.py <folder with *.cobertura.xml> <profile>

A profile names the test run the reports come from: net10.0 measures the core and ASP.NET Core packages, and net48, which
runs on Windows only, measures the System.Web package. Each floor sits about two to three points below the coverage the
package had when the floor was set, so a change that drops a package below it fails the build. Raise a floor when
coverage rises; lower one only with a reason in the commit message. When the folder holds several reports, the lowest
rate of a package among them counts.
"""

import os
import sys
import xml.etree.ElementTree as ElementTree

# Package name -> (floor for lines, floor for branches). None measures a package without a floor.
PROFILES = {
    "net10.0": {
        "Polhem.OAuth2": (0.95, 0.92),
        "Polhem.OAuth2.AspNetCore": (0.93, 0.91),
    },
    "net48": {
        "Polhem.OAuth2.AspNet": (0.92, 0.90),
    },
}


def read_rates(reports: list[str]) -> dict[str, tuple[float, float]]:
    """Returns the lowest (line rate, branch rate) of each package among the reports."""
    rates = {}
    for report in reports:
        for package in ElementTree.parse(report).getroot().iter("package"):
            name = package.get("name")
            measured = (float(package.get("line-rate")), float(package.get("branch-rate")))
            rates[name] = min(rates[name], measured) if name in rates else measured
    return rates


def main(folder: str, profile: str) -> int:
    floors = PROFILES[profile]
    reports = [
        os.path.join(root, name)
        for root, _, names in os.walk(folder)
        for name in names
        if name.endswith(".cobertura.xml")
    ]
    if not reports:
        print(f"No Cobertura report was found under {folder}.")
        return 1

    rates = read_rates(reports)

    lines = [
        "| Package | Lines | Branches | Floors (lines, branches) |",
        "|---------|------:|---------:|-------------------------:|",
    ]
    failed = False
    for name, floor in floors.items():
        if name not in rates:
            print(f"The report has no coverage for {name}.")
            return 1
        line_rate, branch_rate = rates[name]
        if floor is None:
            floor_text = "not set"
        else:
            line_floor, branch_floor = floor
            below = [kind for kind, rate, minimum in (("lines", line_rate, line_floor), ("branches", branch_rate, branch_floor)) if rate < minimum]
            failed |= bool(below)
            floor_text = f"{line_floor:.0%}, {branch_floor:.0%}" + (f" (below for {' and '.join(below)})" if below else "")
        lines.append(f"| {name} | {line_rate:.1%} | {branch_rate:.1%} | {floor_text} |")

    summary = "\n".join([f"## Code coverage ({profile})", "", *lines, ""])
    print(summary)
    if "GITHUB_STEP_SUMMARY" in os.environ:
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as output:
            output.write(summary)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1], sys.argv[2]))

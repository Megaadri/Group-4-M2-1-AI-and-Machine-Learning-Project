import shutil
import sys
from pathlib import Path

LOG_DIR = Path("../Project/EpisodeLogs")

def archive_run(run_id):
    config_path = Path("configs") / (run_id + ".yaml")
    run_folder = Path("data") / run_id

    if not config_path.exists():
        raise SystemExit("no yaml file for run")
    if run_folder.exists():
        raise SystemExit("run folder already exists")

    csv_files = list(LOG_DIR.glob("*.csv"))
    if len(csv_files) == 0:
        raise SystemExit("no csv files ")

    run_folder.mkdir(parents=True)
    for path in csv_files:
        shutil.move(str(path), str(run_folder/path.name))
    shutil.copy(str(config_path), str(run_folder/ "configuration.yaml"))
    print("moved")


def main():
    if len(sys.argv) == 2:
        archive_run(sys.argv[1])

    else :
        print("nope")


if __name__ == "__main__":
    main()
import copy
from pathlib import Path
import random
import csv
import re 
import yaml
import sys


LEARNING_RATES = [1e-4, 3e-4, 6e-4, 1e-3, 3e-3]
BETAS = [1e-3, 3e-3, 1e-2, 3e-2, 1e-1]
BATCH_SIZES = [128, 256, 512, 1024, 2048]
BUFFER_MULTIPLE = 10

SEEDS = [1]

HYPERPARAMETERS = ["batch_size", "buffer_size", "learning_rate", "beta", "epsilon", "num_epoch"]
COLS = ["seed", "max_steps", "batch_size", "buffer_size", "learning_rate", "beta", "epsilon", "lambd", "num_epoch", "time_horizon"]


def generate_config(base, settings):
    config = copy.deepcopy(base)
    behavior = config["behaviors"]["DungeonEscape"]

    if not config.get("env_settings"):
        config["env_settings"] = {}
    config["env_settings"]["seed"] = settings["seed"]
    hyperparams = behavior["hyperparameters"]

    for param in HYPERPARAMETERS:
        if param in settings:
            hyperparams[param] = settings[param]

    return config 

def read_settings(config):
    behavior = config["behaviors"]["DungeonEscape"]
    hyperparams = behavior["hyperparameters"]
    return {
        "seed": (config.get("env_settings").get("seed")),
        "max_steps": behavior.get("max_steps"),
        "batch_size": hyperparams.get("batch_size"),
        "buffer_size": hyperparams.get("buffer_size"),
        "learning_rate": hyperparams.get("learning_rate"),
        "beta": hyperparams.get("beta"),
        "epsilon": hyperparams.get("epsilon"),
        "lambd": hyperparams.get("lambd"),
        "num_epoch": hyperparams.get("num_epoch"),
        "time_horizon": behavior.get("time_horizon")
    }

def signature(settings):
    return tuple(settings[c] for c in COLS if c!= "seed")



def load_yamls():
    runs = []
    for path in sorted(Path("configs").glob("*.yaml")):
        with open(path) as file:
            runs.append((path.stem, read_settings(yaml.safe_load(file))))
    return runs

def grid_settings():
    grid = []
    for lr in LEARNING_RATES: 
        for beta in BETAS:
            for bs in BATCH_SIZES:
                grid.append({
                    "learning_rate": lr, 
                    "beta": beta,
                    "batch_size": bs, 
                    "buffer_size": bs*BUFFER_MULTIPLE
                })
    random.Random(1).shuffle(grid)
    return grid

def build_table():
    runs = load_yamls()

    old = {}
    if Path("yamls_table.csv").exists():
        with open("yamls_table.csv", newline ="") as file:
            for row in csv.DictReader(file):
                old[row["id"]] = (row.get("assigned_to", ""), row.get("status", ""))

    seen = []
    for run_id, settings in runs:
        key = tuple(settings[col] for col in COLS)
        if key in seen: 
            print("Identical settings.")
        seen.append(key)


    with open("yamls_table.csv", "w", newline="") as file:
        writer = csv.writer(file)
        writer.writerow(["id", "assigned_to", "status"] + COLS)
        for run_id, settings in runs:
            person,status = old.get(run_id, ("",""))
            writer.writerow([run_id, person, status] + [settings[c] for c in COLS])
    print("wrote", len(runs), "runs to:", "yamls_table.csv")


def add_runs(base_path, n_new):
    with open(base_path) as file:
        base = yaml.safe_load(file)
    Path("configs").mkdir(exist_ok=True)

    existing = load_yamls()
    used = []
    for run_id, settings in existing:
        used.append((signature(settings), settings["seed"]))

    numbers = []
    for run_id, settings in existing:
        m = re.fullmatch(r"run_(\d+)", run_id)
        if m: 
            numbers.append(int(m.group(1)))
    last_number = max(numbers, default=0)

    new_runs = []
    for combo in grid_settings():
        if len(new_runs) >= n_new:
            break
        for seed in SEEDS:
            if len(new_runs) >= n_new:
                break

            settings = combo.copy()
            settings["seed"] = seed 
            sig = signature(read_settings(generate_config(base, settings)))
            if (sig, seed) in used:
                continue
            used.append((sig, seed))
            new_runs.append(settings)

    for i, settings in enumerate(new_runs, start=1):
        number = last_number+ i
        path = Path("configs") / ("run_%03d.yaml" % number)
        if path.exists():
            raise SystemExit()
        with open (path, "w") as file: 
            yaml.safe_dump(generate_config(base,settings), file, sort_keys=False)



def main():
    if len(sys.argv) == 2 and sys.argv[1] == "--table":
        build_table()
    elif len(sys.argv) == 3:
        add_runs(sys.argv[1], int(sys.argv[2]))
        build_table()
    else:
        print("nope")


if __name__ == "__main__":
    main()
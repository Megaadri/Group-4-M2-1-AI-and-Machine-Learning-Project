from pathlib import Path
import pandas as pd
import numpy as np
import yaml
import sys



EARLY_END = 1/3
FINAL_START = 5/6
THRESHOLD = 0.8 # for now , change later on 

def read_settings(folder):
    path = folder / "configuration.yaml"
    if not path.exists():
        print("no configuration file found")
        return {}

    with open(path) as file:
        config = yaml.safe_load(file)

    behavior = config["behaviors"]["DungeonEscape"]
    hyperparams = behavior.get("hyperparameters", {})

    settings = {}
    settings["seed"] = config.get("env_settings", {}).get("seed")
    settings["max_steps"] = behavior.get("max_steps")
    settings["learning_rate"] = hyperparams.get("learning_rate")
    settings["batch_size"] = hyperparams.get("batch_size")
    settings["buffer_size"] = hyperparams.get("buffer_size")
    settings["beta"] = hyperparams.get("beta")
    settings["epsilon"] = hyperparams.get("epsilon")
    settings["lambd"] = hyperparams.get("lambd")
    settings["num_epoch"] = hyperparams.get("num_epoch")
    settings["time_horizon"] = behavior.get("time_horizon")

    return settings

def summarize_runs(folder):
    episode = pd.read_csv(folder/ "episode_log.csv")
    agent = pd.read_csv(folder / "agent_log.csv")

    episode["success"] = (episode["end_reason"] == "door_unlocked").astype(int)

    dragon_deaths = agent[agent["death_cause"]=="dragon"].groupby(["run_id", "episode"]).size()
    dragon_deaths = dragon_deaths.rename("dragon_deaths").reset_index()
    episode = episode.merge(dragon_deaths, on=["run_id", "episode"], how = "left")
    episode["sacrificed"] = (episode["dragon_deaths"].fillna(0)>0).astype(int)

    episode["position"] = episode["episode"] / episode.groupby("run_id")["episode"].transform("max")
    episode = episode.sort_values("position").reset_index(drop=True)

    row = {}
    row["training_run"] = folder.name
    row.update(read_settings(folder))
    row["num_episodes"] = len(episode)


    final = episode[episode["position"] >= FINAL_START]
    if len(final) > 0:
        row["final_success_rate"] = round(final["success"].mean(), 3)
        row["final_success"] = int(row["final_success_rate"] >= THRESHOLD)
        row["final_sacrifice_rate"] = round(final["sacrificed"].mean(), 3)
    else:
        row["final_success_rate"] = np.nan
        row["final_success"] = np.nan
        row["final_sacrifice_rate"] = np.nan



    before = episode[episode["position"] < EARLY_END]
    if len(before) > 0:
        row["early_success_rate"] = round(before["success"].mean(), 3)
        row["early_sacrifice_rate"] = round(before["sacrificed"].mean(), 3)

    else: 
        row["early_success_rate"] = np.nan
        row["early_sacrifice_rate"] = np.nan


    return row

def build_table():
    rows = []
    for folder in sorted(Path("data").iterdir()):
        if folder.is_dir() and (folder / "episode_log.csv").exists():
            rows.append(summarize_runs(folder))

    table = pd.DataFrame(rows)
    table.to_csv("runs_table.csv", index=False)
    print("wrote them runs")


def main():
    if len(sys.argv) == 1:
        build_table()
    else:
        print("wrong")

   

if __name__ == "__main__":
    main()

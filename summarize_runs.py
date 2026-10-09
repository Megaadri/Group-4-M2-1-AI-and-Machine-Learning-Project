#!/usr/bin/env python3
"""
Usage:
  python summarize_runs.py
  python summarize_runs.py --episode episode_log.csv --agent agent_log.csv \
                           --npc npc_log.csv --out run_summary.csv
"""
import argparse
import pandas as pd

END_REASONS = ["door_unlocked", "baddie_touched_block", "hazard"]
NO_DEATH_LABEL = "none"


def top_players(series_by_agent: pd.Series) -> tuple[str, int]:
    best = series_by_agent.max()
    if best == 0:
        return "n/a", 0
    names = sorted(series_by_agent[series_by_agent == best].index)
    return " | ".join(names), int(best)


def to_bool(s: pd.Series) -> pd.Series:
    if s.dtype == bool:
        return s
    return s.astype(str).str.strip().str.lower().isin(["true", "1", "yes"])


def build_summary(episode_path, agent_path, npc_path=None) -> pd.DataFrame:
    ep = pd.read_csv(episode_path)
    ag = pd.read_csv(agent_path)
    ag["got_key"] = to_bool(ag["got_key"])

    g = ep.groupby("run_id")
    summary = pd.DataFrame(index=g.size().index)
    summary.index.name = "run_id"

    summary["num_episodes"] = g.size()

    reasons = END_REASONS + sorted(set(ep["end_reason"]) - set(END_REASONS))
    counts = (
        pd.crosstab(ep["run_id"], ep["end_reason"])
        .reindex(columns=reasons, fill_value=0)
    )
    for r in reasons:
        summary[f"{r}_count"] = counts[r]
    for r in reasons:
        summary[f"{r}_rate"] = (counts[r] / summary["num_episodes"]).round(4)

    first_win = ep[ep["end_reason"] == "door_unlocked"].groupby("run_id")["episode"].min()
    summary["first_success_episode"] = first_win  # NaN if the run never succeeded
    summary["first_success_episode"] = summary["first_success_episode"].astype("Int64")

    summary["avg_steps_taken"] = g["steps_taken"].mean().round(2)
    summary["avg_players_remaining"] = g["num_players_remaining"].mean().round(3)

    summary["num_dragons"] = g["num_dragons"].max()

    keys = ag.groupby(["run_id", "agent_name"])["got_key"].sum()
    deaths = (
        ag.assign(died=ag["death_cause"].astype(str) != NO_DEATH_LABEL)
        .groupby(["run_id", "agent_name"])["died"]
        .sum()
    )

    key_top = {rid: top_players(s.droplevel("run_id")) for rid, s in keys.groupby("run_id")}
    death_top = {rid: top_players(s.droplevel("run_id")) for rid, s in deaths.groupby("run_id")}

    summary["top_key_picker"] = pd.Series({k: v[0] for k, v in key_top.items()})
    summary["top_key_picker_pickups"] = pd.Series({k: v[1] for k, v in key_top.items()})
    summary["most_deaths_player"] = pd.Series({k: v[0] for k, v in death_top.items()})
    summary["most_deaths_player_deaths"] = pd.Series({k: v[1] for k, v in death_top.items()})

    if npc_path:
        npc = pd.read_csv(npc_path)
        summary["avg_npc_walk_speed"] = npc.groupby("run_id")["walk_speed"].mean().round(3)

    return summary.reset_index()


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawTextHelpFormatter)
    p.add_argument("--episode", default="episode_log.csv")
    p.add_argument("--agent", default="agent_log.csv")
    p.add_argument("--npc", default="npc_log.csv")
    p.add_argument("--out", default="run_summary.csv")
    args = p.parse_args()

    summary = build_summary(args.episode, args.agent, args.npc)
    summary.to_csv(args.out, index=False)
    print(f"Wrote {len(summary)} runs to {args.out}")


if __name__ == "__main__":
    main()

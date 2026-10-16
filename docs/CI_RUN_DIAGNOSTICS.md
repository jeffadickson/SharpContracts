# GitHub Actions Run Visibility Diagnostic

This document describes how to determine the actual CI result for SharpContracts.

## The diagnostic distinction

- GitHub **commit statuses** (`/repos/{owner}/{repo}/commits/{sha}/status`) are not the same as **check runs** (`/repos/{owner}/{repo}/commits/{sha}/check-runs`).
- The currently available GitHub connector's `fetch_commit_workflow_runs` action is documented to return only runs triggered by **pull_request** events. Most SharpContracts commits have gone directly to `main`, triggering **push** events. An empty result is therefore inconclusive.
- A pull request triggers a supported workflow event and may permit reading a run ID. Use the returned run ID with the GitHub connector's run-jobs and job-logs actions.

## GitHub UI path

1. Open https://github.com/jeffadickson/SharpContracts/actions
2. Choose the latest **CI** workflow run; its URL ends in `/actions/runs/<run_id>`
3. Look at **CI Gate** for named upstream status conclusions
4. For a failure, inspect the first failed job and download `sharpcontracts-build-log` or the TRX artifacts

## GitHub CLI alternative

```sh
gh run list -R jeffadickson/SharpContracts -w CI --limit 10
gh run view <run_id> -R jeffadickson/SharpContracts --json status,conclusion,jobs
gh run view <run_id> -R jeffadickson/SharpContracts --log-failed
```

A passing check must be observed, not inferred from committed workflow YAML. This PR intentionally exercises the pull_request event as a diagnostic of the connector's filtering limitation.

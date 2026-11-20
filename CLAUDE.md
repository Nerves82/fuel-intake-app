# Instructions for Claude sessions

Read PROJECT_NOTES.md first: it holds the decisions, privacy rules and business rules.

- Never add real client data anywhere (code, tests, fixtures, logs, commits). Test data must be obviously fake.
- Keep host-specific code out of `FuelIntake.Core` and `FuelIntake.UI`; use the Core interfaces.
- Port business rules from `reference/workbook/` faithfully; when a rule is unclear, ask Steve and record the answer in PROJECT_NOTES.md.
- Add or update tests in `tests/` for every Core change.
- Work on a branch and push it; Steve reviews and merges. CI (GitHub Actions, Windows) must be green.
- Steve's environment: NEKCA laptops are locked down (no admin installs). The app ships as a portable self-contained folder.

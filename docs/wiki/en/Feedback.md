![Feedback](images/en/Feedback1.png)

This tab lets you post bug reports and feature requests to GitHub.

## What it shows

- "Bug reports and feature requests": Click it to open the submission page on GitHub. Your environment, such as the app version, is already filled in.
- "Detected issues": The list of items that the app has detected. Each item shows its type, number, and the time it was detected.

| Type | Description |
|---|---|
| Unnamed skill | A skill whose row name in Skill Details could not be determined. |
| Skill name mismatch | A skill whose row name in Skill Details was determined in two different ways. |
| Class detection mismatch | An effect or skill for which the class was detected in two different ways. |

## Controls

- Check items and click "Report selected" to check whether the same report already exists in Issues for the selected items and open a submission page with the items that have not been reported in the body. If they do not fit on one page, all the pages are opened, split up. Check the content on each page and click "Create" to complete the report. Items included in a submission page are unchecked.
- Items for which the same report exists become "Reported" and can no longer be checked. If all the selected items are "Reported", no submission page is opened.
- Click "Check report status" to check all the items in the list again. Items for which the same report exists become "Reported", and items whose report no longer exists return to unreported. When the check is finished, all items that are not reported are checked. No submission page is opened.
- If the check cannot be done (for example, when you cannot connect to the internet), no submission page is opened.
- When there are many items, or when you click repeatedly, the check may take a while to finish. While it is checking, you can stop it with "Cancel check".

## Notes

- Reports are public on GitHub, and posting requires a GitHub account.
- The body does not include information about other players.
- "Reported" means that the same report exists in Issues. Even after you post, it does not appear until the next check.
- The list and the log are deleted when the app version changes.
- If you are asked for the log, attach `Data\CombatRuntime.log` in the app folder. The log may contain your PC user name.

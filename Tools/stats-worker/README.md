# Last Tide play statistics

The game (`Assets/Scripts/Game/App/Telemetry.cs`) POSTs batches of anonymous events to this Worker, which checks them
and stores them in the D1 database `lasttide-stats` (`schema.sql`). Nothing can be read over HTTP; query with wrangler
(logged in to the Rushing Technologies Cloudflare account: `npx wrangler login`).

```bash
cd Tools/stats-worker
npx wrangler deploy                                   # after editing worker.js
npx wrangler d1 execute lasttide-stats --remote --command "SELECT type, COUNT(*) FROM events WHERE test = 0 GROUP BY type"
```

Useful queries (`test = 0` leaves out TestFlight and debug builds):

```sql
-- The funnel: how many players reached each guide step (where they stop is where the count drops).
SELECT step_index, step, COUNT(DISTINCT user) AS players FROM events
WHERE test = 0 AND type = 'Guide.stepReached' GROUP BY step_index ORDER BY step_index;

-- The furthest step each player got to, and how many stopped there.
SELECT last, COUNT(*) AS players FROM (SELECT user, MAX(step_index) AS last FROM events WHERE test = 0 GROUP BY user)
GROUP BY last ORDER BY last;

-- Passing out, by step and island.
SELECT step, location, COUNT(*) FROM events WHERE test = 0 AND type = 'Survival.passedOut' GROUP BY step, location ORDER BY 3 DESC;

-- Frame rate by device model.
SELECT model, ROUND(AVG(value), 1) AS fps, COUNT(*) FROM events WHERE type = 'Performance.sample' GROUP BY model ORDER BY 2;

-- Players per day.
SELECT substr(received, 1, 10) AS day, COUNT(DISTINCT user) FROM events WHERE test = 0 GROUP BY day ORDER BY day;
```

Limits: 50 events per request, 64 KB per body, 40 payload keys; a malformed batch is refused whole (422). A daily
cron deletes events older than a year. No IP addresses or request headers are stored.

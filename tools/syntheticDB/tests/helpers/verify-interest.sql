-- Independent numeric reference over daily posted balances for 100 accounts.
-- Run with psql -At -v fixture_year=2025. Zero means every monthly payout agrees.
WITH accounts AS (
    SELECT "Id", "InterestRate" FROM banking.savings_accounts ORDER BY "Id" LIMIT 100
), dates AS (
    SELECT d::date AS day FROM generate_series(make_date(:fixture_year,1,1)::timestamp,
        make_date(:fixture_year,12,31)::timestamp, interval '1 day') d
), changes AS (
    SELECT l."AccountId", l."CreatedAt"::date AS day,
        SUM(l."Amount") AS total,
        SUM(l."Amount") FILTER (WHERE l."Type" IN ('interest','tax')) AS payout,
        SUM(l."Amount") FILTER (WHERE l."Type"='interest') AS interest,
        SUM(l."Amount") FILTER (WHERE l."Type"='tax') AS tax
    FROM banking.ledger_entries l JOIN accounts a ON a."Id"=l."AccountId"
    GROUP BY l."AccountId", l."CreatedAt"::date
), balances AS (
    SELECT a."Id", a."InterestRate", d.day, c.interest, c.tax,
        SUM(COALESCE(c.total,0)) OVER (PARTITION BY a."Id" ORDER BY d.day)
            - COALESCE(c.payout,0) AS interest_balance
    FROM accounts a CROSS JOIN dates d LEFT JOIN changes c ON c."AccountId"=a."Id" AND c.day=d.day
), months AS (
    SELECT "Id", date_trunc('month',day) AS month,
        ROUND(SUM(GREATEST(interest_balance,0)) * "InterestRate" /
            (make_date(:fixture_year+1,1,1)-make_date(:fixture_year,1,1)), 2) AS expected,
        COALESCE(SUM(interest),0) AS actual, COALESCE(SUM(tax),0) AS actual_tax
    FROM balances GROUP BY "Id", "InterestRate", date_trunc('month',day)
)
SELECT COUNT(*) FROM months WHERE expected<>actual OR actual_tax<>-ROUND(actual*0.30,2);

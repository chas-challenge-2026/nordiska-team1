CREATE TABLE customers (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100),
    email VARCHAR(100) UNIQUE,
    password_md5 VARCHAR(32)
);

CREATE TABLE savings_accounts (
    id SERIAL PRIMARY KEY,
    customer_id INT REFERENCES customers(id),
    account_number VARCHAR(20),
    balance DECIMAL(15,2) DEFAULT 0,
    interest_rate DECIMAL(5,4) DEFAULT 0.0350,
    account_type VARCHAR(20) DEFAULT 'savings',
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE transactions (
    id SERIAL PRIMARY KEY,
    account_id INT REFERENCES savings_accounts(id),
    type VARCHAR(20),
    amount DECIMAL(15,2),
    balance_after DECIMAL(15,2),
    created_at TIMESTAMP DEFAULT NOW()
);

-- Seed data (password = "password123", MD5 hashed)
-- MD5("password123") = 482c811da5d5b4bc6d497ffa98491e38
INSERT INTO customers (name, email, password_md5) VALUES
('Anna Lindqvist', 'anna@example.com', '482c811da5d5b4bc6d497ffa98491e38'),
('Erik Johansson', 'erik@example.com', '482c811da5d5b4bc6d497ffa98491e38');

INSERT INTO savings_accounts (customer_id, account_number, balance, interest_rate) VALUES
(1, 'NKM-10001', 125000.00, 0.0350),
(1, 'NKM-10002', 45000.00, 0.0280),
(2, 'NKM-20001', 89500.00, 0.0350);

INSERT INTO transactions (account_id, type, amount, balance_after) VALUES
(1, 'deposit', 125000.00, 125000.00),
(2, 'deposit', 45000.00, 45000.00),
(3, 'deposit', 89500.00, 89500.00);

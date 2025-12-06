-- Replace REPLACE_WITH_PASSWORD with a strong password before running.
-- Adjust the host (here it's '%' to allow any host) if you want to restrict connections.

-- 1) Create the user (MySQL 8+ syntax)
CREATE USER IF NOT EXISTS 'thienduong_chatbot_user'@'%' IDENTIFIED BY 'REPLACE_WITH_PASSWORD';

-- 2) Grant only SELECT privileges on the application database
GRANT SELECT ON `thienduong_LTDB`.* TO 'thienduong_chatbot_user'@'%';

-- 3) Apply changes
FLUSH PRIVILEGES;

-- Optional: show grants
SHOW GRANTS FOR 'thienduong_chatbot_user'@'%';

-- Notes:
-- - If your intended username includes an @ or other special characters, split into user and host
--   parts when creating the user: e.g. 'username'@'host'. The connection string's User must be the username only.
-- - After creating the user, update `appsettings.json` -> TextToSql.ConnectionString with the real password.
-- - For tighter security, set the host to the specific app server IP or 'localhost' instead of '%'.

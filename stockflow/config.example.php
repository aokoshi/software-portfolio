<?php
return [
    'host' => getenv('STOCKFLOW_DB_HOST') ?: '127.0.0.1',
    'port' => (int)(getenv('STOCKFLOW_DB_PORT') ?: 3306),
    'database' => getenv('STOCKFLOW_DB_NAME') ?: 'stockflow',
    'username' => getenv('STOCKFLOW_DB_USER') ?: 'root',
    'password' => getenv('STOCKFLOW_DB_PASSWORD') ?: '',
];

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'news_category') THEN
        CREATE TYPE news_category AS ENUM (
            'geopolitics',
            'gold',
            'crypto',
            'viral',
            'market'
        );
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'signal_action') THEN
        CREATE TYPE signal_action AS ENUM (
            'buy',
            'sell',
            'hold'
        );
    END IF;
END$$;

CREATE TABLE IF NOT EXISTS news_sources (
    id              BIGSERIAL PRIMARY KEY,
    name            TEXT NOT NULL UNIQUE,
    base_url        TEXT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS news_articles (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_id           BIGINT NOT NULL REFERENCES news_sources(id) ON DELETE RESTRICT,
    source_external_id  TEXT NULL,
    source_name         TEXT NOT NULL,
    title               TEXT NOT NULL,
    summary             TEXT NOT NULL,
    url                 TEXT NOT NULL,
    published_at        TIMESTAMPTZ NOT NULL,
    category            news_category NOT NULL,
    sentiment_score     NUMERIC(5,4) NULL,
    keywords            TEXT[] NULL,
    raw_payload         JSONB NULL,
    ingested_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at         TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_news_articles_published_at
    ON news_articles (published_at DESC);

CREATE INDEX IF NOT EXISTS ix_news_articles_category_published_at
    ON news_articles (category, published_at DESC);

CREATE INDEX IF NOT EXISTS ix_news_articles_source_name_published_at
    ON news_articles (source_name, published_at DESC);

CREATE TABLE IF NOT EXISTS news_signals (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    news_category       news_category NOT NULL,
    symbol              TEXT NOT NULL,
    action              signal_action NOT NULL,
    confidence          NUMERIC(5,4) NOT NULL,
    suggested_price     NUMERIC(18,6) NOT NULL,
    reason              TEXT NOT NULL,
    generated_at        TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_news_signals_generated_at
    ON news_signals (generated_at DESC);

CREATE INDEX IF NOT EXISTS ix_news_signals_symbol_generated_at
    ON news_signals (symbol, generated_at DESC);

CREATE TABLE IF NOT EXISTS ingestion_runs (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_name         TEXT NOT NULL,
    started_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    finished_at         TIMESTAMPTZ NULL,
    status              TEXT NOT NULL DEFAULT 'running',
    items_fetched       INT NOT NULL DEFAULT 0,
    items_inserted      INT NOT NULL DEFAULT 0,
    error_message       TEXT NULL
);

CREATE INDEX IF NOT EXISTS ix_ingestion_runs_source_name_started_at
    ON ingestion_runs (source_name, started_at DESC);

INSERT INTO news_sources (name, base_url)
VALUES
    ('Polymarket', 'https://polymarket.com')
ON CONFLICT (name) DO NOTHING;

INSERT INTO news_sources (name, base_url)
VALUES
    ('Kalshi', 'https://kalshi.com')
ON CONFLICT (name) DO NOTHING;

COMMIT;

/** @typedef {'debug'|'info'|'warn'|'error'} LogLevel */

const LEVEL_ORDER = { debug: 0, info: 1, warn: 2, error: 3 };

/** @type {LogLevel} */
const MIN_LEVEL = process.env.QA_LOG_LEVEL === 'debug' ? 'debug' : 'info';

export class QaLogger {
  /** @param {string} correlationId @param {string} [suite] */
  constructor(correlationId, suite = 'engine') {
    this.correlationId = correlationId;
    this.suite = suite;
    /** @type {object[]} */
    this.entries = [];
  }

  /** @param {string} suite */
  child(suite) {
    const child = new QaLogger(this.correlationId, suite);
    child.entries = this.entries;
    return child;
  }

  /**
   * @param {LogLevel} level
   * @param {string} message
   * @param {Record<string, unknown>} [meta]
   */
  log(level, message, meta = {}) {
    if (LEVEL_ORDER[level] < LEVEL_ORDER[MIN_LEVEL]) return;

    const entry = {
      timestamp: new Date().toISOString(),
      level,
      correlation_id: this.correlationId,
      suite: this.suite,
      message,
      ...meta,
    };

    this.entries.push(entry);

    const line = JSON.stringify(entry);
    if (level === 'error') {
      console.error(line);
    } else if (level === 'warn') {
      console.warn(line);
    } else if (process.env.QA_LOG_FORMAT !== 'plain') {
      console.log(line);
    } else {
      console.log(`[${level}] [${this.suite}] ${message}`);
    }
  }

  info(message, meta) {
    this.log('info', message, meta);
  }

  warn(message, meta) {
    this.log('warn', message, meta);
  }

  error(message, meta) {
    this.log('error', message, meta);
  }

  debug(message, meta) {
    this.log('debug', message, meta);
  }
}

import { Component, Input, OnChanges } from '@angular/core';

type StructuredRowKind =
  | 'string'
  | 'number'
  | 'boolean'
  | 'null'
  | 'empty-object'
  | 'empty-array'
  | 'special';

type StructuredRow = {
  key: string;
  value: string;
  kind: StructuredRowKind;
};

type StructuredState =
  | { mode: 'empty' }
  | { mode: 'primitive'; value: string; mono: boolean }
  | { mode: 'rows'; rows: StructuredRow[] };

@Component({
  selector: 'app-structured-data-viewer',
  standalone: true,
  templateUrl: './structured-data-viewer.component.html',
  styleUrl: './structured-data-viewer.component.scss',
})
export class StructuredDataViewerComponent implements OnChanges {
  @Input() value: unknown;
  @Input() emptyLabel = '-';
  @Input() maxRows = 220;
  @Input() maxHeight = '16rem';
  @Input() parseJsonStrings = true;
  @Input() forceLtr = false;

  state: StructuredState = { mode: 'empty' };

  ngOnChanges(): void {
    this.state = this.buildState(this.value);
  }

  trackRow(index: number, row: StructuredRow): string {
    return `${index}:${row.key}`;
  }

  isMonospace(kind: StructuredRowKind): boolean {
    return kind !== 'string';
  }

  private buildState(raw: unknown): StructuredState {
    const normalized = this.normalizeInput(raw);

    if (normalized === null || normalized === undefined) {
      return { mode: 'empty' };
    }

    if (!this.isObjectLike(normalized)) {
      const primitive = this.formatPrimitive(normalized);
      if (!primitive.trim()) {
        return { mode: 'empty' };
      }

      return {
        mode: 'primitive',
        value: primitive,
        mono: typeof normalized !== 'string',
      };
    }

    const rows = this.flattenToRows(normalized, this.normalizeMaxRows(this.maxRows));
    if (rows.length === 0) {
      return { mode: 'empty' };
    }

    return { mode: 'rows', rows };
  }

  private normalizeInput(raw: unknown): unknown {
    if (raw === null || raw === undefined) {
      return null;
    }

    if (typeof raw !== 'string') {
      return raw;
    }

    const trimmed = raw.trim();
    if (!trimmed) {
      return null;
    }

    if (!this.parseJsonStrings || !this.looksLikeJson(trimmed)) {
      return raw;
    }

    try {
      return JSON.parse(trimmed) as unknown;
    } catch {
      return raw;
    }
  }

  private flattenToRows(root: unknown, maxRows: number): StructuredRow[] {
    const rows: StructuredRow[] = [];
    const visited = new WeakSet<object>();

    const append = (value: unknown, path: string): boolean => {
      if (rows.length >= maxRows) {
        return false;
      }

      if (value === null) {
        rows.push({ key: this.withFallbackKey(path, 'value'), value: 'null', kind: 'null' });
        return true;
      }

      if (typeof value === 'string') {
        rows.push({ key: this.withFallbackKey(path, 'value'), value, kind: 'string' });
        return true;
      }

      if (typeof value === 'number') {
        rows.push({ key: this.withFallbackKey(path, 'value'), value: String(value), kind: 'number' });
        return true;
      }

      if (typeof value === 'boolean') {
        rows.push({ key: this.withFallbackKey(path, 'value'), value: value ? 'true' : 'false', kind: 'boolean' });
        return true;
      }

      if (Array.isArray(value)) {
        if (value.length === 0) {
          rows.push({ key: this.withFallbackKey(path, '[]'), value: '[]', kind: 'empty-array' });
          return true;
        }

        for (let index = 0; index < value.length; index += 1) {
          const childPath = this.composeArrayPath(path, index);
          if (!append(value[index], childPath)) {
            return false;
          }
        }

        return true;
      }

      if (this.isObjectLike(value)) {
        if (visited.has(value)) {
          rows.push({ key: this.withFallbackKey(path, 'value'), value: '[Circular]', kind: 'special' });
          return true;
        }

        visited.add(value);

        const entries = Object.entries(value);
        if (entries.length === 0) {
          rows.push({ key: this.withFallbackKey(path, '{}'), value: '{}', kind: 'empty-object' });
          return true;
        }

        for (const [key, child] of entries) {
          const childPath = this.composeObjectPath(path, key);
          if (!append(child, childPath)) {
            return false;
          }
        }

        return true;
      }

      rows.push({
        key: this.withFallbackKey(path, 'value'),
        value: this.formatPrimitive(value),
        kind: 'special',
      });
      return true;
    };

    append(root, '');
    return rows;
  }

  private composeObjectPath(parent: string, key: string): string {
    const identifierRegex = /^[A-Za-z_][A-Za-z0-9_]*$/;
    const child = identifierRegex.test(key) ? key : `["${key.replaceAll('"', '\\"')}"]`;
    if (!parent) {
      return child;
    }

    return child.startsWith('[') ? `${parent}${child}` : `${parent}.${child}`;
  }

  private composeArrayPath(parent: string, index: number): string {
    return `${parent}[${index}]`;
  }

  private looksLikeJson(value: string): boolean {
    const first = value[0];
    const last = value[value.length - 1];
    return (first === '{' && last === '}') || (first === '[' && last === ']');
  }

  private isObjectLike(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null;
  }

  private withFallbackKey(path: string, fallback: string): string {
    return path || fallback;
  }

  private normalizeMaxRows(value: number): number {
    if (!Number.isFinite(value) || value < 1) {
      return 220;
    }

    return Math.floor(value);
  }

  private formatPrimitive(value: unknown): string {
    if (value === null || value === undefined) {
      return '';
    }

    if (typeof value === 'string') {
      return value;
    }

    if (typeof value === 'number' || typeof value === 'boolean' || typeof value === 'bigint') {
      return String(value);
    }

    if (typeof value === 'symbol') {
      return value.description ? `Symbol(${value.description})` : 'Symbol()';
    }

    if (typeof value === 'function') {
      return '[Function]';
    }

    try {
      return JSON.stringify(value);
    } catch {
      return String(value);
    }
  }
}

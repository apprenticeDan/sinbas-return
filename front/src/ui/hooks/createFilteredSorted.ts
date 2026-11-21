import { createMemo, type Accessor } from 'solid-js';

export type SortKey<T> = (item: T) => string | number | null;

export interface SortConfig<T> {
  key: SortKey<T>;
  direction: 'asc' | 'desc';
}

export interface SearchConfig<T> {
  value: Accessor<string>;
  keys: Array<(item: T) => string | null>;
}

export interface FilterConfig<T> {
  value: Accessor<string>;
  match: (item: T, value: string) => boolean;
}

export interface FilteredSortedConfig<T> {
  search?: SearchConfig<T>;
  filters?: FilterConfig<T>[];
  sort?: Accessor<SortConfig<T> | null>;
}

export function createFilteredSorted<T>(
  source: Accessor<T[]>,
  config: FilteredSortedConfig<T>
): Accessor<T[]> {
  return createMemo(() => {
    let result = [...source()];

    // Search filter
    if (config.search) {
      const term = config.search.value().trim().toLowerCase();
      if (term) {
        result = result.filter((item) =>
          config.search!.keys.some((k) => {
            const val = k(item);
            return val !== null && val !== undefined && val.toLowerCase().includes(term);
          })
        );
      }
    }

    // Specific filters (AND logic)
    if (config.filters && config.filters.length > 0) {
      for (const filter of config.filters) {
        const filterVal = filter.value();
        if (filterVal !== undefined && filterVal !== null && filterVal.trim() !== '') {
          result = result.filter((item) => filter.match(item, filterVal));
        }
      }
    }

    // Sorting
    const sortConfig = config.sort ? config.sort() : null;
    if (sortConfig) {
      const { key, direction } = sortConfig;
      result.sort((a, b) => {
        const valA = key(a);
        const valB = key(b);

        // Nulls always at the end in both asc and desc
        if (valA === null && valB === null) return 0;
        if (valA === null) return 1;
        if (valB === null) return -1;

        let cmp = 0;
        if (typeof valA === 'number' && typeof valB === 'number') {
          cmp = valA - valB;
        } else {
          cmp = String(valA).localeCompare(String(valB));
        }

        return direction === 'desc' ? -cmp : cmp;
      });
    }

    return result;
  });
}

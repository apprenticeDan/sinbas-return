import { describe, it, expect } from 'vitest';
import { createSignal, createRoot } from 'solid-js';
import {
  createFilteredSorted,
  type SortConfig,
  type FilterConfig,
} from './createFilteredSorted';

interface Item {
  id: number;
  nombre: string;
  categoria?: string | null;
  stock?: number | null;
}

const sampleItems: Item[] = [
  { id: 1, nombre: 'Mara', categoria: 'Maderable', stock: 10 },
  { id: 2, nombre: 'Cedro', categoria: 'Maderable', stock: 5 },
  { id: 3, nombre: 'Tajibo', categoria: 'Ornamental', stock: null },
  { id: 4, nombre: 'Almendra', categoria: 'Frutal', stock: 20 },
];

describe('createFilteredSorted', () => {
  it('1. Sin filtros -> source intacto', () => {
    createRoot((dispose) => {
      const [source] = createSignal(sampleItems);
      const filtered = createFilteredSorted(source, {});
      expect(filtered()).toEqual(sampleItems);
      dispose();
    });
  });

  it('2. Search match 1 key', () => {
    createRoot((dispose) => {
      const [source] = createSignal(sampleItems);
      const [search] = createSignal('Mara');
      const filtered = createFilteredSorted(source, {
        search: {
          value: search,
          keys: [(item) => item.nombre],
        },
      });
      expect(filtered()).toEqual([
        { id: 1, nombre: 'Mara', categoria: 'Maderable', stock: 10 },
      ]);
      dispose();
    });
  });

  it('3. Search sin match -> []', () => {
    createRoot((dispose) => {
      const [source] = createSignal(sampleItems);
      const [search] = createSignal('Inexistente');
      const filtered = createFilteredSorted(source, {
        search: {
          value: search,
          keys: [(item) => item.nombre],
        },
      });
      expect(filtered()).toEqual([]);
      dispose();
    });
  });

  it('4. Search case-insensitive', () => {
    createRoot((dispose) => {
      const [source] = createSignal(sampleItems);
      const [search] = createSignal('cEdRo');
      const filtered = createFilteredSorted(source, {
        search: {
          value: search,
          keys: [(item) => item.nombre],
        },
      });
      expect(filtered()).toEqual([
        { id: 2, nombre: 'Cedro', categoria: 'Maderable', stock: 5 },
      ]);
      dispose();
    });
  });

  it('5. Filter 1 campo', () => {
    createRoot((dispose) => {
      const [source] = createSignal(sampleItems);
      const [cat] = createSignal('Maderable');
      const filtered = createFilteredSorted(source, {
        filters: [
          {
            value: cat,
            match: (item, val) => item.categoria === val,
          },
        ],
      });
      expect(filtered()).toEqual([
        { id: 1, nombre: 'Mara', categoria: 'Maderable', stock: 10 },
        { id: 2, nombre: 'Cedro', categoria: 'Maderable', stock: 5 },
      ]);
      dispose();
    });
  });

  it('6. Filter 2 campos (AND)', () => {
    createRoot((dispose) => {
      const [source] = createSignal(sampleItems);
      const [cat] = createSignal('Maderable');
      const [minStock] = createSignal('8');
      const filters: FilterConfig<Item>[] = [
        {
          value: cat,
          match: (item, val) => item.categoria === val,
        },
        {
          value: minStock,
          match: (item, val) => (item.stock ?? 0) >= Number(val),
        },
      ];
      const filtered = createFilteredSorted(source, { filters });
      expect(filtered()).toEqual([
        { id: 1, nombre: 'Mara', categoria: 'Maderable', stock: 10 },
      ]);
      dispose();
    });
  });

  it('7. Sort ASC string', () => {
    createRoot((dispose) => {
      const [source] = createSignal(sampleItems);
      const [sort] = createSignal<SortConfig<Item> | null>({
        key: (item) => item.nombre,
        direction: 'asc',
      });
      const filtered = createFilteredSorted(source, { sort });
      const nombres = filtered().map((i) => i.nombre);
      expect(nombres).toEqual(['Almendra', 'Cedro', 'Mara', 'Tajibo']);
      dispose();
    });
  });

  it('8. Sort DESC number + nulls al final', () => {
    createRoot((dispose) => {
      const [source] = createSignal(sampleItems);
      const [sort] = createSignal<SortConfig<Item> | null>({
        key: (item) => item.stock ?? null,
        direction: 'desc',
      });
      const filtered = createFilteredSorted(source, { sort });
      const ids = filtered().map((i) => i.id);
      // Stocks: 20 (Almendra id 4), 10 (Mara id 1), 5 (Cedro id 2), null (Tajibo id 3 al final)
      expect(ids).toEqual([4, 1, 2, 3]);
      dispose();
    });
  });
});

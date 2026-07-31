import { describe, expect, it } from 'vitest';
import { FACES, nextFace, pipGrid, pipsOf, total } from './dice';

describe('pipsOf', () => {
  it.each(FACES)('وجه %i همان تعداد خال دارد', (face) => {
    expect(pipsOf(face)).toHaveLength(face);
  });

  it('وجه‌های فرد خالِ وسط دارند و زوج‌ها ندارند', () => {
    for (const face of FACES) {
      expect(pipsOf(face).includes(4)).toBe(face % 2 === 1);
    }
  });

  /** خال‌ها باید قرینه باشند، وگرنه تاس کج به نظر می‌رسد. */
  it('چیدمان هر وجه نسبت به مرکز قرینه است', () => {
    for (const face of FACES) {
      const cells = new Set(pipsOf(face));
      for (const cell of cells) {
        expect(cells.has(8 - cell)).toBe(true);
      }
    }
  });

  it('تاسِ نینداخته هیچ خالی ندارد', () => {
    expect(pipsOf(null)).toHaveLength(0);
  });

  it('وجهِ بی‌معنا خالی برمی‌گردد نه اینکه بترکد', () => {
    expect(pipsOf(0)).toHaveLength(0);
    expect(pipsOf(7)).toHaveLength(0);
  });
});

describe('pipGrid', () => {
  it('همیشه نُه خانه است', () => {
    expect(pipGrid(3)).toHaveLength(9);
    expect(pipGrid(null)).toHaveLength(9);
  });

  it('خانه‌های روشن همان‌هایی هستند که وجه می‌گوید', () => {
    expect(pipGrid(2)).toEqual([true, false, false, false, false, false, false, false, true]);
  });
});

describe('nextFace', () => {
  /**
   * با انتخابِ کاملاً تصادفی، از هر شش بار یک بار همان وجه دوباره می‌آمد و
   * چرخش یک تپش بی‌حرکت می‌ماند.
   */
  it('هرگز همان وجهِ فعلی را برنمی‌گرداند', () => {
    for (const face of FACES) {
      for (let step = 0; step < 6; step++) {
        expect(nextFace(face, () => step / 6)).not.toBe(face);
      }
    }
  });

  it('همیشه یک وجهِ معتبر است', () => {
    for (let step = 0; step < 20; step++) {
      const face = nextFace(3, () => step / 20);
      expect(FACES).toContain(face as (typeof FACES)[number]);
    }
  });

  /** ‎Math.random()‎ می‌تواند خیلی نزدیک به ۱ برگردد؛ نباید از فهرست بیرون بزند. */
  it('با تصادفِ لبه‌ای هم از فهرست بیرون نمی‌زند', () => {
    expect(nextFace(1, () => 0.999999999)).toBe(6);
    expect(nextFace(6, () => 0.999999999)).toBe(5);
  });

  it('از تاسِ نینداخته هم شروع می‌شود', () => {
    expect(FACES).toContain(nextFace(null, () => 0) as (typeof FACES)[number]);
  });
});

describe('total', () => {
  it('مجموع دو تاس', () => {
    expect(total(3, 4)).toBe(7);
  });

  it('پیش از انداختن مجموعی نیست', () => {
    expect(total(null, 4)).toBeNull();
    expect(total(3, null)).toBeNull();
  });
});

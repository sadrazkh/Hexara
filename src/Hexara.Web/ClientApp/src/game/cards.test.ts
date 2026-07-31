import { describe, expect, it } from 'vitest';
import { developmentPiles, edgeKey, freeRoadChoices } from './cards';
import type { LegalMoves, RoadAt } from './connection';

function road(q: number, r: number, side: number): RoadAt {
  return { q, r, side, playerIndex: 0 };
}

function legal(free: RoadAt[], followUps: Record<string, RoadAt[]> = {}): LegalMoves {
  return {
    isMyTurn: true,
    settlements: [],
    roads: [],
    cities: [],
    robberTargets: [],
    freeRoads: free,
    followUpRoads: followUps,
    playableCards: [],
  };
}

describe('edgeKey', () => {
  it('مختصات منفی را با منهای ASCII می‌نویسد', () => {
    // کلید باید با همانی که سرور می‌سازد مو نزند؛ منهای فارسی اینجا فاجعه است.
    expect(edgeKey({ q: -1, r: -2, side: 0 })).toBe('-1,-2,0');
  });
});

describe('freeRoadChoices', () => {
  it('برای انتخاب اول همان فهرست سرور است', () => {
    const moves = legal([road(0, 0, 0), road(0, 0, 1)]);

    expect(freeRoadChoices(moves, [])).toHaveLength(2);
  });

  it('یالی که انتخاب شده دوباره پیشنهاد نمی‌شود', () => {
    const moves = legal([road(0, 0, 0), road(0, 0, 1)]);

    const left = freeRoadChoices(moves, ['0,0,0']);

    expect(left.map(edgeKey)).toEqual(['0,0,1']);
  });

  /** بی این، کارت جاده‌سازی هرگز نمی‌توانست زنجیره بسازد. */
  it('جاهایی را که جاده‌ی اول باز کرده اضافه می‌کند', () => {
    const moves = legal([road(0, 0, 0)], { '0,0,0': [road(1, -1, 2)] });

    const left = freeRoadChoices(moves, ['0,0,0']);

    expect(left.map(edgeKey)).toEqual(['1,-1,2']);
  });

  it('یالی که هم در فهرست اول است و هم در فهرست بعدی، دوبار نمی‌آید', () => {
    const moves = legal([road(0, 0, 0), road(0, 0, 1)], { '0,0,0': [road(0, 0, 1), road(1, -1, 2)] });

    const left = freeRoadChoices(moves, ['0,0,0']);

    expect(left.map(edgeKey)).toEqual(['0,0,1', '1,-1,2']);
  });

  it('وقتی سرور برای این انتخاب چیزی نفرستاده، فقط بقیه‌ی فهرست می‌ماند', () => {
    const moves = legal([road(0, 0, 0), road(0, 0, 1)]);

    expect(freeRoadChoices(moves, ['0,0,0']).map(edgeKey)).toEqual(['0,0,1']);
  });
});

describe('developmentPiles', () => {
  /**
   * دو دسته‌ی سرور از هم جدا هستند. ریختنشان روی هم با ‎spread‎ عددِ غلط می‌داد و
   * بازیکن یکی از شوالیه‌هایش را گم می‌کرد.
   */
  it('کارت آماده و کارت تازه را جمع می‌کند نه جایگزین', () => {
    const piles = developmentPiles({ Knight: 1 }, { Knight: 2 });

    expect(piles).toEqual([{ kind: 'Knight', count: 3, fresh: 2 }]);
  });

  it('کارتی که فقط تازه است هم یک دسته می‌شود', () => {
    expect(developmentPiles({}, { Monopoly: 1 })).toEqual([{ kind: 'Monopoly', count: 1, fresh: 1 }]);
  });

  it('دسته‌ی صفر نشان داده نمی‌شود', () => {
    expect(developmentPiles({ Knight: 0 }, { Knight: 0 })).toEqual([]);
  });

  it('نبودنِ هر دو دسته یعنی هیچ کارتی', () => {
    expect(developmentPiles(null, undefined)).toEqual([]);
  });

  it('کارتی که تازه ندارد، تازه‌ی صفر می‌گیرد', () => {
    expect(developmentPiles({ VictoryPoint: 2 }, {})).toEqual([
      { kind: 'VictoryPoint', count: 2, fresh: 0 },
    ]);
  });
});

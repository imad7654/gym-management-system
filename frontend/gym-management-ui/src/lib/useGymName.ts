import { useQuery } from '@tanstack/react-query';
import { gymInfoService } from '@services/gymInfoService';
import { GYM, gymLabel } from '@/config/gym';

/**
 * What to call this gym on screen.
 *
 * The saved name from Gym settings wins, so an owner who renames the gym sees it everywhere
 * without a developer. `src/config/gym.ts` is the fallback, which covers the two cases the
 * database cannot: the moment before the first request returns, and a fresh install whose
 * owner has not opened Settings yet.
 *
 * Without the fallback a clone would show a blank sidebar on first load and then snap to a
 * name, which reads as a bug. With a hardcoded fallback it would show the wrong gym's name,
 * which is worse.
 */
export const useGymName = (): { name: string; label: string } => {
  const { data } = useQuery({
    queryKey: ['gym-info'],
    queryFn: () => gymInfoService.getGymInfo(),

    // The name changes about once in the life of an install, so re-asking on every screen
    // is pure noise on a desk machine that stays open all day.
    staleTime: 1000 * 60 * 60,
    retry: false,
  });

  // The seeded row carries its own mark, so an emoji is not added twice.
  const saved = data?.gymName?.trim();
  const name = saved && saved.length > 0 ? saved : GYM.name;
  const alreadyMarked = GYM.mark.length > 0 && name.startsWith(GYM.mark);

  return { name, label: alreadyMarked ? name : gymLabel(name) };
};

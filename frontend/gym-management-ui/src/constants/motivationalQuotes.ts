export interface MotivationalQuote {
  text: string;
  subtext: string;
  iconType: 'fire' | 'fitness' | 'gymnastics';
}

export const MOTIVATIONAL_QUOTES: MotivationalQuote[] = [
  {
    text: "No Pain, No Gain",
    subtext: "Every drop of sweat brings you closer to greatness",
    iconType: 'fire'
  },
  {
    text: "If You're Not Gonna Do It, No One Will",
    subtext: "Your transformation is in your hands alone",
    iconType: 'fitness'
  },
  {
    text: "Train Insane or Remain The Same",
    subtext: "Comfort zones are where dreams go to die",
    iconType: 'gymnastics'
  },
];

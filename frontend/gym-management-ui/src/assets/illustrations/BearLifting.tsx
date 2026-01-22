import { Box, SxProps, Theme } from '@mui/material';

interface BearLiftingProps {
  sx?: SxProps<Theme>;
}

export const BearLifting = ({ sx }: BearLiftingProps) => {
  const bearIllustration = `
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 800 600">
      <!-- Bear body (brown) -->
      <ellipse cx="400" cy="400" rx="120" ry="140" fill="#654321"/>
      <!-- Bear head -->
      <circle cx="400" cy="280" r="80" fill="#654321"/>
      <!-- Bear ears -->
      <circle cx="350" cy="240" r="30" fill="#654321"/>
      <circle cx="450" cy="240" r="30" fill="#654321"/>
      <!-- Bear snout -->
      <ellipse cx="400" cy="300" rx="40" ry="30" fill="#8B6914"/>
      <!-- Bear nose -->
      <circle cx="400" cy="295" r="12" fill="#000"/>
      <!-- Bear eyes -->
      <circle cx="375" cy="270" r="8" fill="#000"/>
      <circle cx="425" cy="270" r="8" fill="#000"/>
      <!-- Bear arms (muscular) -->
      <ellipse cx="320" cy="320" rx="35" ry="90" fill="#654321" transform="rotate(-30 320 320)"/>
      <ellipse cx="480" cy="320" rx="35" ry="90" fill="#654321" transform="rotate(30 480 320)"/>
      <!-- Barbell (heavy, bending) -->
      <path d="M 200 180 Q 400 220 600 180" stroke="#333" stroke-width="15" fill="none"/>
      <!-- Weight plates (left) -->
      <circle cx="180" cy="175" r="40" fill="#1b5e20" stroke="#2e7d32" stroke-width="3"/>
      <circle cx="180" cy="175" r="30" fill="#2e7d32"/>
      <!-- Weight plates (right) -->
      <circle cx="620" cy="175" r="40" fill="#1b5e20" stroke="#2e7d32" stroke-width="3"/>
      <circle cx="620" cy="175" r="30" fill="#2e7d32"/>
      <!-- Sweat drops -->
      <ellipse cx="340" cy="260" rx="5" ry="8" fill="#4dd0e1" opacity="0.7"/>
      <ellipse cx="460" cy="260" rx="5" ry="8" fill="#4dd0e1" opacity="0.7"/>
    </svg>
  `;

  return (
    <Box
      sx={sx}
      dangerouslySetInnerHTML={{ __html: bearIllustration }}
    />
  );
};

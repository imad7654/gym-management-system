import { Card, CardContent, Typography, Box } from '@mui/material';
import { GYM, gymTint } from '@/config/gym';
import { Package } from '@app-types/index';

interface PackageCardProps {
  package: Package;
  index: number;
}

export const PackageCard = ({ package: pkg, index }: PackageCardProps) => {
  return (
    <Card
      sx={{
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        transition: 'all 0.3s',
        border: '3px solid transparent',
        borderRadius: 3,
        overflow: 'hidden',
        '&:hover': {
          transform: 'translateY(-12px) scale(1.02)',
          boxShadow: `0 16px 32px ${gymTint(0.2)}`,
          borderColor: GYM.colour.main,
        },
      }}
    >
      <Box
        sx={{
          background: index % 2 === 0
            ? `linear-gradient(135deg, ${GYM.colour.main}, ${GYM.colour.dark})`
            : `linear-gradient(135deg, ${GYM.colour.dark}, ${GYM.colour.deepest})`,
          color: 'white',
          py: 3,
          textAlign: 'center',
          position: 'relative',
        }}
      >
        <Typography variant="h5" fontWeight="bold" sx={{ textTransform: 'uppercase' }}>
          {pkg.name}
        </Typography>
      </Box>
      <CardContent sx={{ flexGrow: 1, textAlign: 'center', pt: 4, pb: 4, bgcolor: 'white' }}>
        <Typography
          variant="h2"
          sx={{
            color: GYM.colour.main,
            fontWeight: 900,
            mb: 1
          }}
        >
          ${pkg.price?.toFixed(2)}
        </Typography>
        <Typography variant="body1" color="text.secondary" sx={{ mb: 3, fontWeight: 'bold' }}>
          {pkg.durationDays} days
        </Typography>
        <Typography variant="body2" sx={{ color: '#666', lineHeight: 1.6 }}>
          {pkg.description || 'Full access to all gym facilities and equipment'}
        </Typography>
      </CardContent>
    </Card>
  );
};

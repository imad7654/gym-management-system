import { Paper, Typography, Box } from '@mui/material';
import FitnessCenterIcon from '@mui/icons-material/FitnessCenter';
import LocalFireDepartmentIcon from '@mui/icons-material/LocalFireDepartment';
import SportsGymnasticsIcon from '@mui/icons-material/SportsGymnastics';
import { MotivationalQuote } from '@constants/motivationalQuotes';

interface MotivationalQuoteCardProps {
  quote: MotivationalQuote;
}

const getIcon = (iconType: string) => {
  const iconProps = { sx: { fontSize: 50 } };

  switch (iconType) {
    case 'fire':
      return <LocalFireDepartmentIcon {...iconProps} />;
    case 'fitness':
      return <FitnessCenterIcon {...iconProps} />;
    case 'gymnastics':
      return <SportsGymnasticsIcon {...iconProps} />;
    default:
      return <FitnessCenterIcon {...iconProps} />;
  }
};

export const MotivationalQuoteCard = ({ quote }: MotivationalQuoteCardProps) => {
  return (
    <Paper
      elevation={0}
      sx={{
        p: 4,
        textAlign: 'center',
        bgcolor: '#f1f8f4',
        border: '4px solid #2e7d32',
        borderRadius: 3,
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        transition: 'all 0.4s',
        position: 'relative',
        overflow: 'hidden',
        '&:hover': {
          transform: 'translateY(-10px) scale(1.03)',
          boxShadow: '0 12px 24px rgba(46, 125, 50, 0.25)',
          borderColor: '#1b5e20',
          bgcolor: 'white',
        },
        '&::before': {
          content: '""',
          position: 'absolute',
          top: 0,
          left: 0,
          right: 0,
          height: '6px',
          background: 'linear-gradient(90deg, #2e7d32, #1b5e20)',
        }
      }}
    >
      <Box sx={{ color: '#2e7d32', mb: 2, animation: 'pulse 2s infinite' }}>
        {getIcon(quote.iconType)}
      </Box>
      <Typography
        variant="h4"
        sx={{
          fontWeight: 900,
          color: '#1b5e20',
          mb: 2,
          textTransform: 'uppercase',
          lineHeight: 1.2,
        }}
      >
        "{quote.text}"
      </Typography>
      <Typography
        variant="body1"
        sx={{
          color: '#555',
          fontStyle: 'italic',
          fontSize: '0.95rem'
        }}
      >
        {quote.subtext}
      </Typography>
    </Paper>
  );
};

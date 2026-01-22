import { Container, Typography, Box, Button, Grid } from '@mui/material';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { packageService } from '@services/packageService';
import { BearLifting } from '@assets/illustrations/BearLifting';
import { MOTIVATIONAL_QUOTES } from '@constants/motivationalQuotes';
import { MotivationalQuoteCard } from '@components/home/MotivationalQuoteCard';
import { PackageCard } from '@components/home/PackageCard';

const HomePage = () => {
  const navigate = useNavigate();

  const { data: packages } = useQuery({
    queryKey: ['packages', 'active'],
    queryFn: () => packageService.getActivePackages(),
  });

  return (
    <Box>
      {/* Hero Section with Bear Background */}
      <Box
        sx={{
          background: `linear-gradient(135deg, rgba(46, 125, 50, 0.95) 0%, rgba(27, 94, 32, 0.95) 100%)`,
          color: 'white',
          py: 12,
          textAlign: 'center',
          position: 'relative',
          overflow: 'hidden',
          minHeight: '600px',
          display: 'flex',
          alignItems: 'center',
        }}
      >
        {/* Bear SVG Background */}
        <BearLifting
          sx={{
            position: 'absolute',
            top: '50%',
            left: '50%',
            transform: 'translate(-50%, -50%)',
            width: '600px',
            height: '450px',
            opacity: 0.15,
            zIndex: 0,
          }}
        />

        <Container maxWidth="lg" sx={{ position: 'relative', zIndex: 1 }}>
          <Typography
            variant="h1"
            sx={{
              fontWeight: 900,
              fontSize: { xs: '2.5rem', md: '4.5rem' },
              mb: 2,
              textShadow: '3px 3px 6px rgba(0,0,0,0.4)',
              letterSpacing: '0.02em'
            }}
          >
            🐻 THE FIT BEAR GYM
          </Typography>
          <Typography
            variant="h4"
            sx={{
              mb: 4,
              fontStyle: 'italic',
              fontWeight: 400,
              opacity: 0.95,
              textShadow: '2px 2px 4px rgba(0,0,0,0.3)',
            }}
          >
            "Where Strength Meets Nature"
          </Typography>
          <Typography variant="h6" sx={{ mb: 6, maxWidth: 700, mx: 'auto', opacity: 0.95 }}>
            Train like a bear, dominate like a champion. Join our pack and unleash your primal strength!
          </Typography>
          <Box sx={{ display: 'flex', gap: 3, justifyContent: 'center', flexWrap: 'wrap' }}>
            <Button
              variant="contained"
              size="large"
              onClick={() => navigate('/login')}
              sx={{
                bgcolor: 'white',
                color: '#2e7d32',
                px: 5,
                py: 2,
                fontSize: '1.2rem',
                fontWeight: 'bold',
                boxShadow: '0 4px 12px rgba(0,0,0,0.2)',
                '&:hover': {
                  bgcolor: '#f1f1f1',
                  transform: 'scale(1.08) translateY(-2px)',
                  boxShadow: '0 8px 20px rgba(0,0,0,0.3)',
                },
                transition: 'all 0.3s'
              }}
            >
              🔑 Admin Login
            </Button>
            <Button
              variant="outlined"
              size="large"
              disabled
              sx={{
                borderColor: 'white',
                borderWidth: 2,
                color: 'white',
                px: 5,
                py: 2,
                fontSize: '1.2rem',
                fontWeight: 'bold',
              }}
            >
              💪 Join The Pack (Soon)
            </Button>
          </Box>
        </Container>
      </Box>

      {/* Motivational Wall Quotes Section */}
      <Box sx={{ bgcolor: 'white', py: 8 }}>
        <Container maxWidth="lg">
          <Typography
            variant="h3"
            textAlign="center"
            sx={{
              mb: 6,
              fontWeight: 'bold',
              color: '#1b5e20',
              textTransform: 'uppercase',
              letterSpacing: '0.05em'
            }}
          >
            💪 Wall Of Motivation
          </Typography>
          <Grid container spacing={4}>
            {MOTIVATIONAL_QUOTES.map((quote, index) => (
              <Grid item xs={12} md={4} key={index}>
                <MotivationalQuoteCard quote={quote} />
              </Grid>
            ))}
          </Grid>
        </Container>
      </Box>

      {/* Membership Packages Section - Green Theme */}
      <Box sx={{ bgcolor: '#f5f5f5', py: 8 }}>
        <Container maxWidth="lg">
          <Typography
            variant="h3"
            textAlign="center"
            gutterBottom
            sx={{
              color: '#1b5e20',
              fontWeight: 'bold',
              mb: 2,
              textTransform: 'uppercase'
            }}
          >
            🎯 Membership Packages
          </Typography>
          <Typography
            variant="h6"
            textAlign="center"
            sx={{ mb: 6, color: '#666', fontStyle: 'italic' }}
          >
            Choose Your Path To Greatness
          </Typography>

          <Grid container spacing={4}>
            {packages?.map((pkg, index) => (
              <Grid item xs={12} sm={6} md={3} key={pkg.id}>
                <PackageCard package={pkg} index={index} />
              </Grid>
            ))}
          </Grid>
        </Container>
      </Box>

      {/* Contact/Footer Section */}
      <Box
        sx={{
          background: 'linear-gradient(135deg, #1b5e20 0%, #0d4416 100%)',
          color: 'white',
          py: 6,
          textAlign: 'center'
        }}
      >
        <Container maxWidth="md">
          <Typography variant="h4" gutterBottom sx={{ fontWeight: 'bold' }}>
            📍 Find Us & Join The Pack
          </Typography>
          <Typography variant="body1" sx={{ mb: 1, opacity: 0.9 }}>
            The Fit Bear Gym - Where bears train champions
          </Typography>
          <Typography variant="body1" sx={{ opacity: 0.9 }}>
            📞 Contact us to start your transformation journey
          </Typography>
        </Container>
      </Box>
    </Box>
  );
};

export default HomePage;

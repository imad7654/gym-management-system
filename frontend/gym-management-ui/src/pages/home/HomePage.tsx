import { Container, Typography, Box, Button, Grid, Link, Stack } from '@mui/material';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { packageService } from '@services/packageService';
import { gymInfoService } from '@services/gymInfoService';
import { BearLifting } from '@assets/illustrations/BearLifting';
import { MOTIVATIONAL_QUOTES } from '@constants/motivationalQuotes';
import { MotivationalQuoteCard } from '@components/home/MotivationalQuoteCard';
import { PackageCard } from '@components/home/PackageCard';

/**
 * What the homepage says when the owner has not filled in Gym settings yet. Every one of
 * these is overridden by the saved GymInfo row, so the page never looks unfinished and
 * the fallbacks stay out of the way once real copy exists.
 */
const FALLBACK = {
  gymName: '🐻 THE FIT BEAR GYM',
  heroTitle: 'Where Strength Meets Nature',
  heroSubtitle:
    'Train like a bear, dominate like a champion. Join our pack and unleash your primal strength!',
  aboutTitle: '📍 Find Us & Join The Pack',
  aboutContent: 'The Fit Bear Gym - Where bears train champions',
};

/**
 * Opening hours are free text, but older seeded rows hold a JSON object of day -> hours.
 * Render those as readable lines rather than letting a raw blob onto the public page.
 */
const formatOperatingHours = (hours: string): string => {
  const trimmed = hours.trim();
  if (!trimmed.startsWith('{')) return trimmed;

  try {
    const parsed = JSON.parse(trimmed);
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return trimmed;

    const DAY_ORDER = [
      'monday',
      'tuesday',
      'wednesday',
      'thursday',
      'friday',
      'saturday',
      'sunday',
    ];

    return Object.entries(parsed as Record<string, unknown>)
      .filter(([, value]) => typeof value === 'string')
      .sort(
        ([a], [b]) =>
          DAY_ORDER.indexOf(a.toLowerCase()) - DAY_ORDER.indexOf(b.toLowerCase())
      )
      .map(([day, value]) => `${day[0].toUpperCase()}${day.slice(1)}: ${value}`)
      .join('\n');
  } catch {
    return trimmed;
  }
};

const HomePage = () => {
  const navigate = useNavigate();

  const { data: packages } = useQuery({
    queryKey: ['packages', 'active'],
    queryFn: () => packageService.getActivePackages(),
  });

  // Public endpoint, so this works for visitors who are not logged in. If it fails the
  // page still renders on the fallbacks above.
  const { data: gymInfo } = useQuery({
    queryKey: ['gym-info'],
    queryFn: gymInfoService.getGymInfo,
    retry: false,
  });

  const gymName = gymInfo?.gymName || FALLBACK.gymName;
  const heroTitle = gymInfo?.heroTitle || FALLBACK.heroTitle;
  const heroSubtitle =
    gymInfo?.heroSubtitle || gymInfo?.description || FALLBACK.heroSubtitle;
  const aboutTitle = gymInfo?.aboutTitle || FALLBACK.aboutTitle;
  const aboutContent = gymInfo?.aboutContent || FALLBACK.aboutContent;

  const socials = [
    { label: 'Facebook', url: gymInfo?.facebookUrl },
    { label: 'Instagram', url: gymInfo?.instagramUrl },
    { label: 'X', url: gymInfo?.twitterUrl },
  ].filter((s): s is { label: string; url: string } => Boolean(s.url));

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
            {gymName}
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
            {heroTitle}
          </Typography>
          <Typography variant="h6" sx={{ mb: 6, maxWidth: 700, mx: 'auto', opacity: 0.95 }}>
            {heroSubtitle}
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
            {aboutTitle}
          </Typography>
          <Typography variant="body1" sx={{ mb: 2, opacity: 0.9, whiteSpace: 'pre-line' }}>
            {aboutContent}
          </Typography>

          {gymInfo?.address && (
            <Typography variant="body1" sx={{ opacity: 0.9, whiteSpace: 'pre-line' }}>
              📍 {gymInfo.address}
            </Typography>
          )}
          {gymInfo?.operatingHours && (
            <Typography variant="body1" sx={{ opacity: 0.9, whiteSpace: 'pre-line' }}>
              🕒 {formatOperatingHours(gymInfo.operatingHours)}
            </Typography>
          )}
          {gymInfo?.phoneNumber && (
            <Typography variant="body1" sx={{ opacity: 0.9 }}>
              📞{' '}
              <Link href={`tel:${gymInfo.phoneNumber}`} color="inherit">
                {gymInfo.phoneNumber}
              </Link>
            </Typography>
          )}
          {gymInfo?.email && (
            <Typography variant="body1" sx={{ opacity: 0.9 }}>
              ✉️{' '}
              <Link href={`mailto:${gymInfo.email}`} color="inherit">
                {gymInfo.email}
              </Link>
            </Typography>
          )}
          {!gymInfo?.phoneNumber && !gymInfo?.email && (
            <Typography variant="body1" sx={{ opacity: 0.9 }}>
              📞 Contact us to start your transformation journey
            </Typography>
          )}

          {socials.length > 0 && (
            <Stack
              direction="row"
              spacing={3}
              justifyContent="center"
              sx={{ mt: 3 }}
            >
              {socials.map((social) => (
                <Link
                  key={social.label}
                  href={social.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  color="inherit"
                  sx={{ fontWeight: 'bold' }}
                >
                  {social.label}
                </Link>
              ))}
            </Stack>
          )}
        </Container>
      </Box>
    </Box>
  );
};

export default HomePage;

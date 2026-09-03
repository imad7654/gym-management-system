import { createTheme } from '@mui/material/styles';
import { GYM } from '@/config/gym';

export const theme = createTheme({
  palette: {
    // Every brand colour comes from src/config/gym.ts, so rebranding a clone is one file
    // rather than a hunt through the source for hex values.
    primary: GYM.colour,
    secondary: GYM.accent,

    // Success deliberately matches the brand: in this app the brand colour already means
    // "this went well" — a paid membership, a completed payment — and a second green
    // beside it reads as a mistake.
    success: {
      main: GYM.colour.main,
    },
    error: {
      main: '#d32f2f',
    },
    warning: {
      main: '#ed6c02',
    },
    background: {
      default: '#f5f5f5',
      paper: '#ffffff',
    },
  },
  typography: {
    fontFamily: '"Roboto", "Helvetica", "Arial", sans-serif',
    h1: {
      fontSize: '2.5rem',
      fontWeight: 600,
    },
    h2: {
      fontSize: '2rem',
      fontWeight: 600,
    },
    h3: {
      fontSize: '1.75rem',
      fontWeight: 600,
    },
    h4: {
      fontSize: '1.5rem',
      fontWeight: 600,
    },
    h5: {
      fontSize: '1.25rem',
      fontWeight: 600,
    },
    h6: {
      fontSize: '1rem',
      fontWeight: 600,
    },
  },
  shape: {
    borderRadius: 8,
  },
  components: {
    MuiButton: {
      styleOverrides: {
        root: {
          textTransform: 'none',
          fontWeight: 500,
        },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          boxShadow: '0 2px 4px rgba(0,0,0,0.1)',
        },
      },
    },
  },
});

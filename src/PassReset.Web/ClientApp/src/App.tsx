import { useEffect, useMemo, useState } from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Container from '@mui/material/Container';
import CssBaseline from '@mui/material/CssBaseline';
import Skeleton from '@mui/material/Skeleton';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import useMediaQuery from '@mui/material/useMediaQuery';
import { ThemeProvider, createTheme } from '@mui/material/styles';

import BrandHeader from './components/BrandHeader';
import { ErrorBoundary } from './components/ErrorBoundary';
import { PasswordForm } from './components/PasswordForm';
import { useSettings } from './hooks/useSettings';

function buildTheme(prefersDark: boolean) {
  return createTheme({
    typography: {
      fontFamily: '"Inter", "Roboto", "Helvetica Neue", Arial, sans-serif',
    },
    palette: {
      mode: prefersDark ? 'dark' : 'light',
      primary: {
        main:  '#0b6366',  // WCAG AA compliant (~4.7:1 on white)
        dark:  '#094e51',
        light: '#0d7377',
      },
      ...(prefersDark ? {} : {
        background: {
          default: '#f5f5f7',
        },
      }),
    },
    components: {
      MuiCard: {
        styleOverrides: {
          root: {
            borderRadius: 16,
            boxShadow: prefersDark
              ? '0 8px 32px rgba(0,0,0,0.30)'
              : '0 8px 32px rgba(0,0,0,0.10)',
          },
        },
      },
      MuiTextField: {
        defaultProps: {
          variant: 'outlined',
          size: 'small',
        },
      },
      MuiButton: {
        styleOverrides: {
          root: {
            textTransform: 'none',
            fontWeight: 600,
            borderRadius: 8,
          },
        },
      },
    },
  });
}

export default function App() {
  const prefersDark = useMediaQuery('(prefers-color-scheme: dark)');
  const theme = useMemo(() => buildTheme(prefersDark), [prefersDark]);

  const { settings, loading, error } = useSettings();
  const [succeeded, setSucceeded]    = useState(false);

  // Update page title once settings are loaded
  useEffect(() => {
    if (settings?.applicationTitle) {
      document.title = settings.applicationTitle;
    }
  }, [settings?.applicationTitle]);

  // Inject favicon at runtime when Branding.FaviconFileName is set.
  useEffect(() => {
    const favicon = settings?.branding?.faviconFileName;
    if (!favicon) return;
    const href = `/brand/${favicon}`;
    let link = document.querySelector<HTMLLinkElement>('link[rel="icon"]');
    if (!link) {
      link = document.createElement('link');
      link.rel = 'icon';
      document.head.appendChild(link);
    }
    link.href = href;
  }, [settings?.branding?.faviconFileName]);

  const branding = settings?.branding;
  const helpdeskUrl = branding?.helpdeskUrl;
  const helpdeskEmail = branding?.helpdeskEmail;
  const showHelpdesk = Boolean(helpdeskUrl || helpdeskEmail);

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <ErrorBoundary>
      <Box
        sx={{
          minHeight: '100vh',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          bgcolor: 'background.default',
          py: 4,
          gap: 3,
        }}
      >
        {/* Product name header (FEAT-001 — branded) */}
        <BrandHeader branding={branding} />

        <Container disableGutters sx={{ maxWidth: 440, width: '100%', px: { xs: 2, sm: 0 } }}>
          <Card>
            <CardContent sx={{ p: { xs: 3, sm: 4 } }}>

              {/* Header */}
              <Typography variant="h5" fontWeight={600} gutterBottom>
                {settings?.changePasswordTitle ?? 'Change Account Password'}
              </Typography>

              {/* Operator usage text (FEAT-001) — replaces default helper when set */}
              {branding?.usageText && (
                <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                  {branding.usageText}
                </Typography>
              )}

              {/* Loading skeleton — preserves layout to minimise CLS */}
              {loading && (
                <Box sx={{ py: 1 }}>
                  <Skeleton variant="rounded" height={40} sx={{ mb: 2 }} />
                  <Skeleton variant="rounded" height={40} sx={{ mb: 2 }} />
                  <Skeleton variant="rounded" height={40} sx={{ mb: 2 }} />
                  <Skeleton variant="rounded" height={40} sx={{ mb: 2 }} />
                  <Skeleton variant="rounded" height={42} />
                </Box>
              )}

              {/* Settings load error */}
              {!loading && error && (
                <Alert severity="error">
                  Unable to load application settings. Please refresh the page or contact IT Support.
                </Alert>
              )}

              {/* Success state */}
              {!loading && !error && succeeded && (
                <Box>
                  <Alert severity="success" sx={{ mb: 2 }}>
                    <Typography fontWeight={600}>
                      {settings?.alerts?.successAlertTitle ?? 'Password changed successfully.'}
                    </Typography>
                    {settings?.alerts?.successAlertBody && (
                      <Typography variant="body2" sx={{ mt: 0.5 }}>
                        {settings.alerts.successAlertBody}
                      </Typography>
                    )}
                  </Alert>
                  <Button
                    variant="outlined"
                    onClick={() => setSucceeded(false)}
                    fullWidth
                  >
                    Change another password
                  </Button>
                </Box>
              )}

              {/* Form */}
              {!loading && !error && !succeeded && settings && (
                <PasswordForm settings={settings} onSuccess={() => setSucceeded(true)} />
              )}

            </CardContent>
          </Card>
        </Container>

        {/* Helpdesk block (FEAT-001) — hidden when both URL and email absent */}
        {showHelpdesk && (
          <Stack direction="row" spacing={2} sx={{ color: 'text.secondary' }}>
            {helpdeskUrl && (
              <Typography variant="caption">
                <a
                  href={helpdeskUrl}
                  target="_blank"
                  rel="noopener"
                  style={{ color: 'inherit' }}
                >
                  Helpdesk
                </a>
              </Typography>
            )}
            {helpdeskEmail && (
              <Typography variant="caption">
                <a href={`mailto:${helpdeskEmail}`} style={{ color: 'inherit' }}>
                  {helpdeskEmail}
                </a>
              </Typography>
            )}
          </Stack>
        )}

        {/* Footer */}
        <Typography variant="caption" sx={{ color: 'text.disabled' }}>
          &copy; {new Date().getFullYear()} — {branding?.companyName ?? 'Internal IT Tool'}
        </Typography>
      </Box>
      </ErrorBoundary>
    </ThemeProvider>
  );
}

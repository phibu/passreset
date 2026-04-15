import { useEffect, useState } from 'react';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import LockPersonIcon from '@mui/icons-material/LockPerson';

import type { BrandingSettings } from '../types/settings';

interface Props {
  branding?: BrandingSettings;
}

export default function BrandHeader({ branding }: Props) {
  const portalName = branding?.portalName ?? 'PassReset';
  const logoFile = branding?.logoFileName;
  const [logoFailed, setLogoFailed] = useState(false);

  // Reset failure state when the source file name changes.
  useEffect(() => {
    setLogoFailed(false);
  }, [logoFile]);

  const showLogo = logoFile && !logoFailed;

  return (
    <Stack direction="row" alignItems="center" spacing={1}>
      {showLogo ? (
        <img
          src={`/brand/${logoFile}`}
          alt={portalName}
          onError={() => setLogoFailed(true)}
          style={{ maxHeight: 48, maxWidth: 200, objectFit: 'contain' }}
        />
      ) : (
        <>
          <LockPersonIcon sx={{ fontSize: 28, color: 'primary.main' }} />
          <Typography
            variant="h6"
            fontWeight={700}
            letterSpacing={0.5}
            sx={{ color: 'primary.main', lineHeight: 1 }}
          >
            {portalName}
          </Typography>
        </>
      )}
    </Stack>
  );
}

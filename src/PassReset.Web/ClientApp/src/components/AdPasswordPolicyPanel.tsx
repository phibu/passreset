import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Paper from '@mui/material/Paper';
import Skeleton from '@mui/material/Skeleton';
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';

import type { PolicyResponse } from '../types/settings';

interface Props {
  policy: PolicyResponse | null;
  loading: boolean;
}

export default function AdPasswordPolicyPanel({ policy, loading }: Props) {
  if (loading) {
    return <Skeleton variant="rounded" height={56} sx={{ mb: 2 }} />;
  }

  // Fail closed — render nothing when the policy is unavailable or the feature is off.
  if (!policy) return null;

  const rules: string[] = [`Minimum ${policy.minLength} characters`];
  if (policy.requiresComplexity) {
    rules.push('Must include uppercase, lowercase, number, and symbol');
  }
  if (policy.historyLength > 0) {
    rules.push(`Cannot reuse last ${policy.historyLength} passwords`);
  }

  return (
    <Paper
      variant="outlined"
      role="region"
      aria-label="Password requirements"
      sx={{ p: 1.5, mb: 2 }}
    >
      <List dense disablePadding>
        {rules.map((rule) => (
          <ListItem key={rule} disableGutters sx={{ py: 0.25 }}>
            <ListItemIcon sx={{ minWidth: 32 }}>
              <CheckCircleOutlineIcon fontSize="small" sx={{ color: 'text.secondary' }} />
            </ListItemIcon>
            <ListItemText
              primary={rule}
              primaryTypographyProps={{ variant: 'body2' }}
            />
          </ListItem>
        ))}
      </List>
    </Paper>
  );
}

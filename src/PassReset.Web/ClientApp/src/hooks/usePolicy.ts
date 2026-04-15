import { useEffect, useState } from 'react';

import { fetchPolicy } from '../api/client';
import type { PolicyResponse } from '../types/settings';

export function usePolicy(enabled: boolean) {
  const [policy, setPolicy] = useState<PolicyResponse | null>(null);
  const [loading, setLoading] = useState(enabled);

  useEffect(() => {
    if (!enabled) {
      setLoading(false);
      setPolicy(null);
      return;
    }

    let cancelled = false;
    setLoading(true);
    fetchPolicy().then((p) => {
      if (cancelled) return;
      setPolicy(p);
      setLoading(false);
    });

    return () => {
      cancelled = true;
    };
  }, [enabled]);

  return { policy, loading };
}

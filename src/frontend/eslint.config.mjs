import {defineConfig,globalIgnores} from 'eslint/config';
import nextVitals from 'eslint-config-next/core-web-vitals';
import nextTypeScript from 'eslint-config-next/typescript';

export default defineConfig([
  ...nextVitals,
  ...nextTypeScript,
  {
    rules:{
      '@typescript-eslint/no-explicit-any':'error',
      '@typescript-eslint/no-unused-vars':['warn',{argsIgnorePattern:'^_',varsIgnorePattern:'^_'}],
      'no-console':['warn',{allow:['warn','error']}],
      'react-hooks/exhaustive-deps':'warn',
      'react-hooks/set-state-in-effect':'warn',
    },
  },
  globalIgnores(['.next/**','out/**','build/**','next-env.d.ts']),
]);

import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

const config: Config = {
  title: 'SharpOMatic',
  tagline: 'Build reliable AI workflows faster in .NET',
  favicon: 'img/favicon.ico',

  // Future flags, see https://docusaurus.io/docs/api/docusaurus-config#future
  future: {
    v4: true, // Improve compatibility with the upcoming Docusaurus v4
  },

  // Set the production url of your site here
  url: 'https://sharpomatic.com',
  // Set the /<baseUrl>/ pathname under which your site is served
  // For GitHub pages deployment, it is often '/<projectName>/'
  baseUrl: '/',

  // GitHub pages deployment config.
  // If you aren't using GitHub pages, you don't need these.
  organizationName: 'sharpomatic', // Usually your GitHub org/user name.
  projectName: 'SharpOMatic', // Usually your repo name.

  onBrokenLinks: 'throw',

  // Even if you don't use internationalization, you can use this field to set
  // useful metadata like html lang. For example, if your site is Chinese, you
  // may want to replace "en" with "zh-Hans".
  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
        },
        blog: {
          showReadingTime: false,
        },
        gtag: {
          trackingID: 'G-P1MPCDFX5W',
          anonymizeIP: true,
        },
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    image: 'img/docusaurus-social-card.jpg',
    colorMode: {
      defaultMode: 'light',
      disableSwitch: false,
      respectPrefersColorScheme: true,
    },
    navbar: {
      title: 'SharpOMatic',
      logo: {
        alt: 'SharpOMatic',
        src: 'img/small_dark.png',
        srcDark: 'img/small_light.png',
      },
      items: [
        {
          to: '/docs/',
          position: 'right',
          label: 'Documentation',
        },
        {
          to: '/blog',
          position: 'right',
          label: 'Releases',
        },
        {
          href: 'https://github.com/sharpomatic/SharpOMatic/issues',
          label: 'Support',
          position: 'right',
        },
        {
          href: 'https://github.com/sharpomatic/SharpOMatic',
          label: 'View on GitHub',
          position: 'right',
          className: 'navbar__github-button',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Docs',
          items: [
            {
              label: 'Introduction',
              to: '/docs/',
            },
            {
              label: 'Getting started',
              to: '/docs/getting-started/start-with-nuget-packages',
            },
            {
              label: 'Core concepts',
              to: '/docs/core-concepts/workflows',
            },
          ],
        },
        {
          title: 'Build',
          items: [
            {
              label: 'Model Call node',
              to: '/docs/nodes/model-call-node/text',
            },
            {
              label: 'AG-UI',
              to: '/docs/ag-ui/',
            },
            {
              label: 'Programmatic API',
              to: '/docs/programmatic/run-workflow',
            },
          ],
        },
        {
          title: 'Project',
          items: [
            {
              label: 'GitHub',
              href: 'https://github.com/sharpomatic/SharpOMatic',
            },
            {
              label: 'Release notes',
              to: '/blog',
            },
            {
              label: 'Support',
              href: 'https://github.com/sharpomatic/SharpOMatic/issues',
            },
          ],
        },
      ],
      copyright: `SharpOMatic · Build reliable AI workflows faster in .NET · © ${new Date().getFullYear()} Phil Wright`,
    },
    prism: {
      theme: prismThemes.vsDark,
      darkTheme: prismThemes.vsDark,
      additionalLanguages: ['csharp'],
    },
  } satisfies Preset.ThemeConfig,
};

export default config;

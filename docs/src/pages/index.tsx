import type {ReactNode} from 'react';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import HomepageFeatures from '@site/src/components/HomepageFeatures';
import Heading from '@theme/Heading';

import styles from './index.module.css';
import heroImage from '@site/src/assets/hero.png';

const productPoints = [
  'Shorter iteration cycles',
  'Your runtime and data',
  'Resilience across providers',
  'Quality you can measure',
];

function ArrowIcon(): ReactNode {
  return (
    <svg viewBox="0 0 20 20" aria-hidden="true">
      <path d="M4 10h11M11 6l4 4-4 4" />
    </svg>
  );
}

function GitHubIcon(): ReactNode {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M12 2.7a9.5 9.5 0 0 0-3 18.5c.5.1.7-.2.7-.5v-1.9c-2.8.6-3.4-1.2-3.4-1.2-.5-1.2-1.1-1.5-1.1-1.5-.9-.6.1-.6.1-.6 1 0 1.6 1.1 1.6 1.1.9 1.6 2.4 1.1 2.9.9.1-.7.4-1.1.7-1.4-2.3-.3-4.7-1.1-4.7-5a3.9 3.9 0 0 1 1-2.7c-.1-.3-.4-1.3.1-2.7 0 0 .9-.3 2.8 1a9.5 9.5 0 0 1 5.1 0c1.9-1.3 2.8-1 2.8-1 .5 1.4.2 2.4.1 2.7a3.9 3.9 0 0 1 1 2.7c0 3.9-2.4 4.8-4.7 5 .4.3.7 1 .7 1.9v2.8c0 .3.2.6.7.5A9.5 9.5 0 0 0 12 2.7Z" />
    </svg>
  );
}

function HomepageHeader(): ReactNode {
  return (
    <header className={styles.hero}>
      <div className={styles.heroGlow} aria-hidden="true" />
      <div className={styles.heroGrid}>
        <div className={styles.heroContent}>
          <div className={styles.eyebrow}>
            <span className={styles.eyebrowDot} />
            Open-source AI workflow platform for .NET
          </div>
          <Heading as="h1" className={styles.heroTitle}>
            Ship reliable AI workflows
            <span>Without leaving .NET</span>
          </Heading>
          <p className={styles.heroDescription}>
            Give your team a faster way to design, test, and improve AI behavior
            while keeping execution, data, and security inside the applications
            you control.
          </p>
          <div className={styles.heroActions}>
            <Link
              className={styles.primaryAction}
              to="/docs/getting-started/start-with-nuget-packages">
              Start building
              <ArrowIcon />
            </Link>
            <Link
              className={styles.secondaryAction}
              href="https://github.com/sharpomatic/SharpOMatic">
              <GitHubIcon />
              View on GitHub
            </Link>
          </div>
          <p className={styles.heroNote}>
            Open source. Self-hosted. Built for ASP.NET Core.
          </p>
        </div>

        <div className={styles.productPreview}>
          <div className={styles.previewGlow} aria-hidden="true" />
          <div className={styles.previewWindow}>
            <div className={styles.previewChrome}>
              <div className={styles.windowDots} aria-hidden="true">
                <span />
                <span />
                <span />
              </div>
              <span className={styles.previewLabel}>Visual workflow designer</span>
              <span className={styles.previewStatus}>
                <span /> Live run
              </span>
            </div>
            <img
              className={styles.heroImage}
              src={heroImage}
              alt="SharpOMatic workflow designer showing parallel workflow paths and a successful run trace"
            />
          </div>
        </div>
      </div>

      <div className={styles.productPoints}>
        {productPoints.map((point) => (
          <div className={styles.productPoint} key={point}>
            <span aria-hidden="true">✓</span>
            {point}
          </div>
        ))}
      </div>
    </header>
  );
}

export default function Home(): ReactNode {
  const {siteConfig} = useDocusaurusContext();
  return (
    <Layout
      title={siteConfig.tagline}
      description="Build reliable, self-hosted AI workflows faster while keeping execution, data, and security inside your .NET applications.">
      <div className={styles.page}>
        <HomepageHeader />
        <main>
          <HomepageFeatures />
        </main>
      </div>
    </Layout>
  );
}

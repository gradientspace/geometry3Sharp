# CLA Assistant Setup Guide

This document explains how the CLA (Contributor License Agreement) Assistant is configured for the geometry3Sharp repository.

## Overview

The CLA Assistant automatically manages contributor license agreements for pull requests. When a new contributor opens a PR, they are asked to sign the CLA before their contribution can be merged.

## Implementation

This repository uses the **GitHub Action version** of CLA Assistant, which is the recommended modern approach. The implementation consists of:

### 1. GitHub Workflow (`.github/workflows/cla.yml`)

The workflow is triggered on:
- New pull requests (`pull_request_target`)
- PR updates (synchronize, close)
- Comments on PRs (`issue_comment`)

**Key Features:**
- Automatically checks CLA status on every PR
- Responds to signature comments
- Stores signatures in the repository itself (in `signatures/version1/cla.json`)
- Excludes bot accounts automatically

### 2. CLA Document (`CLA.md`)

The actual Contributor License Agreement that contributors must accept. This document:
- Grants copyright and patent licenses
- Ensures contributors have the right to submit their code
- Protects both contributors and the project

### 3. Contributing Guidelines (`CONTRIBUTING.md`)

Instructions for contributors on how to sign the CLA.

## Configuration Details

### Workflow Configuration

```yaml
path-to-signatures: 'signatures/version1/cla.json'
path-to-document: 'https://github.com/gradientspace/geometry3Sharp/blob/master/CLA.md'
branch: 'master'
allowlist: 'bot*,dependabot*,*[bot]'
```

- **Signatures**: Stored in the `master` branch at `signatures/version1/cla.json`
- **CLA Document**: Links to the CLA.md file in the repository
- **Branch**: Uses the `master` branch for storing signatures (must not be protected)
- **Allowlist**: Automatically exempts bot accounts

### Required Secrets

The workflow requires the following GitHub secrets:

1. **GITHUB_TOKEN** (automatically provided by GitHub)
   - Used for all CLA operations
   - No manual configuration needed

**Note**: The PERSONAL_ACCESS_TOKEN is NOT required for this setup since signatures are stored in the same repository.

## How It Works

1. **New PR Created**: When someone opens a pull request, the workflow runs automatically.

2. **CLA Check**: The bot checks if the contributor has already signed the CLA.

3. **Request Signature**: If not signed, the bot posts a comment asking the contributor to sign.

4. **Contributor Signs**: The contributor comments: `I have read the CLA Document and I hereby sign the CLA`

5. **Signature Recorded**: The bot records the signature in `signatures/version1/cla.json`

6. **Status Updated**: The PR status is updated to show CLA compliance.

## Maintenance

### Updating the CLA

If you need to update the CLA document:

1. Update `CLA.md` with the new terms
2. Update the version in the workflow file: `path-to-signatures: 'signatures/version2/cla.json'`
3. All contributors will need to re-sign under the new version

### Manually Adding Signatures

If needed, you can manually add a signature to `signatures/version1/cla.json`:

```json
{
  "signedContributors": [
    {
      "name": "username",
      "pullRequestNo": 123,
      "comment_id": 456789,
      "created_at": "2026-01-09T15:00:00Z"
    }
  ]
}
```

### Allowlist Management

To exempt specific users from signing the CLA, update the `allowlist` in `.github/workflows/cla.yml`:

```yaml
allowlist: 'bot*,dependabot*,*[bot],specific-user'
```

## Alternative: Hosted CLA Assistant

This repository uses the GitHub Action version, but you can also use the hosted service at [cla-assistant.io](https://cla-assistant.io).

**To switch to the hosted version:**

1. Remove `.github/workflows/cla.yml`
2. Go to https://cla-assistant.io
3. Sign in with GitHub
4. Link your repository
5. Create a GitHub Gist with your CLA text
6. Configure the service to use your Gist

**Note**: The GitHub Action version is recommended because:
- Signatures are stored in your repository (more transparent)
- No external service dependency
- Full control over the workflow
- Free and open source

## Troubleshooting

### Workflow doesn't run
- Check that the workflow file is in the `master` branch
- Verify GitHub Actions are enabled for the repository
- Check the Actions tab for error logs

### Signatures not being recorded
- Ensure the `master` branch is not protected (or adjust branch protection to allow the bot)
- Check that the bot has write permissions
- Verify the signature storage path exists

### Contributors can't sign
- Make sure they're commenting on the PR (not the commit)
- The exact phrase must be used: `I have read the CLA Document and I hereby sign the CLA`
- Check that the workflow has proper permissions

## References

- [CLA Assistant GitHub Action](https://github.com/contributor-assistant/github-action)
- [Original CLA Assistant](https://github.com/cla-assistant/cla-assistant)
- [Example CLA Documents](https://contributoragreements.org/)

## Questions?

For questions about the CLA setup, please open an issue in the repository.

# RepoLens — Product

## Problem

Systematically researching GitHub to find out how saturated a project idea is —
and where actual opportunity remains — is hard. There is no easy way to answer
"has this been done, how crowded is that space, and what would stand out?".

## Primary user

Software developers looking for portfolio/project ideas and wanting to know
which directions are saturated versus which still have room.

## Core future capabilities

- Repository discovery
- Similarity analysis
- Repository clustering
- Novelty analysis
- Portfolio gap analysis
- Personalized project recommendation

## Non-goals

- A GitHub clone or code-hosting platform
- A generic chatbot
- Indexing all of GitHub
- A full-scale general-purpose search engine

## Status

Walking skeleton / infrastructure only. None of the core capabilities above are
implemented yet; the repository currently proves the development chain
(browser → React → ASP.NET Core → PostgreSQL + pgvector) end to end.

# Figma screen-to-feature map

Source: https://www.figma.com/design/we7rb1RMwsyrHybfpw3COl/Untitled?node-id=0-1

The browser exposed the following named screen frames. The current shell follows the visible pale canvas, sidebar, cards, and blue/purple accents. It is not a verified pixel-exact reproduction. Detailed measurements, assets, nested components, mobile variants, and prototype transitions still need inspection.

| Figma screen | Feature | Planned route family | Phase |
|---|---|---|---|
| DevSync - Home & Developer Hub | Home | `/` | 2–4 |
| Explore Technical Blogs | Blogs/Explore | `/blogs` | 4 |
| My Blogs - Author Dashboard | Blogs/MyBlogs | `/my/blogs` | 4 |
| Technical Blog Editor - DevSync | Blogs/Create, Edit, Publish | `/my/blogs/new`, `/my/blogs/{id}/edit` | 4 |
| Understanding ASP.NET Core Middleware - DevSync Reader | Blogs/Read | `/blogs/{slug}` | 4 |
| Study Notes Workspace | StudyNotes/List | `/notes` | 5 |
| Study Note Block Editor - DevSync | StudyNotes/Edit | `/notes/{id}` | 5 |
| Spaced Repetition Revision Dashboard - DevSync | Revision/Dashboard, Review | `/revision` | 5 |
| Todo & Task Management Board - DevSync | Tasks/List, Details | `/tasks` | 6 |
| Todo Kanban Board - DevSync | Tasks/Kanban, Move | `/tasks/board` | 6 |
| Teams & Collaboration Dashboard - DevSync | Teams/List | `/teams` | 7 |
| Core Runtime Architecture - Team Workspace | Teams/Workspace | `/teams/{id}` | 7 |
| Mothish Raj (@mothish) - Developer Profile | Profiles/View, Edit | `/people/{handle}` | 3, 8 |
| Sign In - DevSync Developer Knowledge Platform | Account/SignIn | `/account/sign-in` | 3 |
| Settings & Publisher Verification - DevSync | Settings, Verification | `/account/settings`, `/account/verification` | 8 |
| Admin Moderation Console - DevSync | Administration/Moderation | `/moderation` | 8 |

The additional `Html → Body` layer is not treated as a new product module. Article titles, developer names, and team names in the frames are sample content. Both task screens will operate on the same task model. The team workspace name is sample content, not a requirement to change application runtime architecture.

For every screen, specify success, empty, loading (where relevant), validation, access-denied, and error states before the feature is considered complete. Mobile behavior and keyboard navigation are part of acceptance.

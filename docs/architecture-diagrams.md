# SmakoszWebApp - Architecture Diagrams

## 1. Overall System Architecture

```mermaid
graph TB
    subgraph "Client Layer"
        Browser[Web Browser]
        PWA[PWA/Mobile App]
        Mobile[Future Native App]
    end

    subgraph "ASP.NET Core Web Application"
        subgraph "Presentation Layer"
            MVC[MVC Controllers<br/>HomeController<br/>DishController<br/>ProfileController<br/>ReviewController]
            API[API Controllers<br/>ReviewsApiController<br/>SearchApiController<br/>DishesApiController]
            Views[Razor Views<br/>HTML + JavaScript]
        end

        subgraph "Business Logic Layer"
            Services[Services<br/>DishService<br/>ReviewService<br/>UserService]
            MLClients[ML Service Clients<br/>ContentModerationClient<br/>CommentModerationClient<br/>RecommendationClient]
        end

        subgraph "Data Access Layer"
            Repositories[Repositories<br/>DishRepository<br/>ReviewRepository<br/>UserRepository]
            EF[Entity Framework Core]
        end
    end

    subgraph "ML Microservices (Python/FastAPI)"
        GPUManager[GPU Manager Service<br/>Port 8000<br/>GPU Pool Management]
        ContentMod[Content Moderation Service<br/>Port 8001<br/>NSFW Detection]
        CommentMod[Comment Moderation Service<br/>Port 8002<br/>Toxic Comment Detection]
        Recommendation[Recommendation Service<br/>Port 8003<br/>Collaborative Filtering]
    end

    subgraph "Infrastructure"
        DB[(PostgreSQL Database)]
        Redis[(Redis Cache)]
        RabbitMQ[RabbitMQ<br/>Message Queue]
        Storage[File Storage<br/>Images/Photos]
    end

    subgraph "Hardware"
        GPU[GPU Pool<br/>CUDA Devices]
    end

    %% Client connections
    Browser -->|HTTP GET| MVC
    Browser -->|AJAX/Fetch| API
    PWA -->|HTTP GET| MVC
    PWA -->|AJAX/Fetch| API
    PWA -->|Service Worker| Views
    Mobile -->|REST API| API

    %% MVC flow
    MVC -->|Use| Services
    MVC -->|Return| Views
    Views -->|Displayed in| Browser

    %% API flow
    API -->|Use| Services
    API -->|Return JSON| Browser

    %% Services layer
    Services -->|Call| Repositories
    Services -->|HTTP Request| MLClients

    %% ML Clients to Services
    MLClients -->|HTTP POST| ContentMod
    MLClients -->|HTTP POST| CommentMod
    MLClients -->|HTTP POST| Recommendation
    MLClients -->|Async via| RabbitMQ

    %% ML Services coordination
    ContentMod -->|Acquire GPU| GPUManager
    CommentMod -->|Acquire GPU| GPUManager
    Recommendation -->|Acquire GPU| GPUManager
    GPUManager -->|Allocate| GPU

    %% Data layer
    Repositories -->|Query| EF
    EF -->|SQL| DB
    Services -->|Cache| Redis
    Services -->|Store| Storage

    style MVC fill:#e1f5ff
    style API fill:#fff4e1
    style GPUManager fill:#ffe1f5
    style ContentMod fill:#f0e1ff
    style CommentMod fill:#f0e1ff
    style Recommendation fill:#f0e1ff
```

## 2. MVC Request Flow (Server-Side Rendering)

```mermaid
sequenceDiagram
    participant User as User Browser
    participant MVC as MVC Controller
    participant Service as Business Service
    participant Repo as Repository
    participant DB as Database
    participant View as Razor View

    User->>MVC: GET /Dish/Details/5
    activate MVC

    MVC->>Service: GetDishDetails(5)
    activate Service

    Service->>Repo: GetDishById(5)
    activate Repo

    Repo->>DB: SELECT * FROM Dishes WHERE Id=5
    activate DB
    DB-->>Repo: Dish data
    deactivate DB

    Repo-->>Service: Dish entity
    deactivate Repo

    Service->>Repo: GetReviewsForDish(5)
    activate Repo
    Repo->>DB: SELECT * FROM Reviews WHERE DishId=5
    DB-->>Repo: Reviews data
    Repo-->>Service: Review entities
    deactivate Repo

    Service-->>MVC: DishViewModel
    deactivate Service

    MVC->>View: Return View(model)
    activate View
    View-->>MVC: Rendered HTML
    deactivate View

    MVC-->>User: 200 OK (Complete HTML Page)
    deactivate MVC

    Note over User,View: Complete HTML page with dish details,<br/>reviews, and navigation - ready for SEO
```

## 3. API Request Flow (AJAX/Dynamic Updates)

```mermaid
sequenceDiagram
    participant Browser as Browser JavaScript
    participant API as API Controller
    participant Service as Business Service
    participant Repo as Repository
    participant DB as Database

    Note over Browser: User types in search box

    Browser->>API: GET /api/search/dishes?query=pizza
    activate API

    API->>Service: SearchDishes("pizza")
    activate Service

    Service->>Repo: SearchDishes("pizza")
    activate Repo

    Repo->>DB: SELECT * FROM Dishes<br/>WHERE Name LIKE '%pizza%'
    activate DB
    DB-->>Repo: Matching dishes
    deactivate DB

    Repo-->>Service: Dish entities
    deactivate Repo

    Service-->>API: List<DishViewModel>
    deactivate Service

    API-->>Browser: 200 OK (JSON data)
    deactivate API

    Note over Browser: JavaScript updates UI<br/>without page reload

    Browser->>Browser: Update search results<br/>dynamically in DOM
```

## 4. Review Submission with ML Moderation Flow

```mermaid
sequenceDiagram
    participant User as User Browser
    participant API as ReviewsApiController
    participant Service as ReviewService
    participant CommentClient as CommentModerationClient
    participant CommentML as Comment Moderation Service
    participant ContentClient as ContentModerationClient
    participant ContentML as Content Moderation Service
    participant GPU as GPU Manager
    participant Queue as RabbitMQ
    participant Repo as Repository
    participant DB as Database

    User->>API: POST /api/reviews/add<br/>(review + photos)
    activate API

    API->>Service: CreateReview(model)
    activate Service

    %% Comment moderation
    Service->>CommentClient: ModerateCommentAsync(text)
    activate CommentClient
    CommentClient->>CommentML: POST /moderate-comment
    activate CommentML

    CommentML->>GPU: Acquire GPU
    activate GPU
    GPU-->>CommentML: GPU allocated
    deactivate GPU

    CommentML->>CommentML: Run toxicity model
    CommentML-->>CommentClient: ToxicityResult<br/>(scores by category)
    deactivate CommentML
    CommentClient-->>Service: ModerationResult
    deactivate CommentClient

    alt Comment is toxic
        Service-->>API: Rejected (toxic content)
        API-->>User: 400 Bad Request<br/>(reason: toxic language)
    else Comment is clean
        %% Image moderation (async)
        Service->>ContentClient: ModerateImageAsync(photo)
        activate ContentClient
        ContentClient->>Queue: Publish moderation job
        activate Queue
        deactivate ContentClient

        Queue->>ContentML: Moderation job
        deactivate Queue
        activate ContentML

        ContentML->>GPU: Acquire GPU
        activate GPU
        GPU-->>ContentML: GPU allocated
        deactivate GPU

        ContentML->>ContentML: Run NSFW detection
        ContentML->>Queue: Publish result
        Queue->>Service: Moderation complete
        deactivate ContentML

        %% Save review
        Service->>Repo: SaveReview(review)
        activate Repo
        Repo->>DB: INSERT INTO Reviews...
        activate DB
        DB-->>Repo: Success
        deactivate DB
        Repo-->>Service: Review saved
        deactivate Repo

        Service-->>API: Success
        deactivate Service
        API-->>User: 200 OK<br/>(review created)
        deactivate API
    end
```

## 5. Component Diagram

```mermaid
graph TB
    subgraph "ASP.NET Core Web App"
        subgraph "Controllers"
            MVC_Home[HomeController]
            MVC_Dish[DishController]
            MVC_Profile[ProfileController]
            MVC_Review[ReviewController]
            API_Reviews[ReviewsApiController]
            API_Search[SearchApiController]
            API_Dishes[DishesApiController]
        end

        subgraph "ViewModels"
            VM_Dish[DishViewModel]
            VM_Review[ReviewViewModel]
            VM_Profile[ProfileViewModel]
            VM_AddReview[AddReviewViewModel]
        end

        subgraph "Services"
            Svc_Dish[DishService]
            Svc_Review[ReviewService]
            Svc_User[UserService]
            Svc_Recommendation[RecommendationService]
        end

        subgraph "ML Clients"
            Client_Content[ContentModerationClient]
            Client_Comment[CommentModerationClient]
            Client_Rec[RecommendationClient]
        end

        subgraph "Repositories"
            Repo_Dish[DishRepository]
            Repo_Review[ReviewRepository]
            Repo_User[UserRepository]
        end

        subgraph "Data Models"
            Model_Dish[Dish]
            Model_Review[Review]
            Model_User[User]
        end
    end

    %% MVC Controller connections
    MVC_Home -->|uses| Svc_Dish
    MVC_Home -->|uses| Svc_Review
    MVC_Dish -->|uses| Svc_Dish
    MVC_Dish -->|uses| Svc_Review
    MVC_Profile -->|uses| Svc_User
    MVC_Profile -->|uses| Svc_Review
    MVC_Review -->|uses| Svc_Review

    %% API Controller connections
    API_Reviews -->|uses| Svc_Review
    API_Search -->|uses| Svc_Dish
    API_Dishes -->|uses| Svc_Dish

    %% Service connections
    Svc_Review -->|uses| Client_Comment
    Svc_Review -->|uses| Client_Content
    Svc_Review -->|uses| Repo_Review
    Svc_Dish -->|uses| Repo_Dish
    Svc_User -->|uses| Repo_User
    Svc_Recommendation -->|uses| Client_Rec

    %% Repository connections
    Repo_Dish -->|CRUD| Model_Dish
    Repo_Review -->|CRUD| Model_Review
    Repo_User -->|CRUD| Model_User

    %% ViewModel usage
    MVC_Home -->|returns| VM_Dish
    MVC_Dish -->|returns| VM_Dish
    MVC_Profile -->|returns| VM_Profile
    API_Reviews -->|accepts| VM_AddReview
    API_Reviews -->|returns| VM_Review

    style MVC_Home fill:#e1f5ff
    style MVC_Dish fill:#e1f5ff
    style MVC_Profile fill:#e1f5ff
    style MVC_Review fill:#e1f5ff
    style API_Reviews fill:#fff4e1
    style API_Search fill:#fff4e1
    style API_Dishes fill:#fff4e1
```

## 6. Deployment Architecture (Docker Compose)

```mermaid
graph TB
    subgraph "Docker Host"
        subgraph "Application Network"
            WebApp[ASP.NET Core Web App<br/>Container: smakosz-webapp<br/>Port: 5000]

            subgraph "ML Services Network"
                GPU_Mgr[GPU Manager Service<br/>Container: gpu-manager<br/>Port: 8000]
                Content[Content Moderation<br/>Container: content-moderation<br/>Port: 8001]
                Comment[Comment Moderation<br/>Container: comment-moderation<br/>Port: 8002]
                Rec[Recommendation Service<br/>Container: recommendation<br/>Port: 8003]
            end

            subgraph "Data Layer"
                Postgres[(PostgreSQL<br/>Container: postgres<br/>Port: 5432)]
                RedisDB[(Redis<br/>Container: redis<br/>Port: 6379)]
                Rabbit[RabbitMQ<br/>Container: rabbitmq<br/>Port: 5672, 15672]
            end

            subgraph "Storage"
                Volumes[Docker Volumes<br/>- db-data<br/>- redis-data<br/>- photo-storage]
            end
        end

        subgraph "GPU Resources"
            GPU0[GPU 0<br/>CUDA Device]
            GPU1[GPU 1<br/>CUDA Device]
        end
    end

    subgraph "External"
        Client[Web Browsers<br/>Mobile Apps]
    end

    %% Client connections
    Client -->|HTTP/HTTPS| WebApp

    %% WebApp connections
    WebApp -->|HTTP| Content
    WebApp -->|HTTP| Comment
    WebApp -->|HTTP| Rec
    WebApp -->|SQL| Postgres
    WebApp -->|Cache| RedisDB
    WebApp -->|Messages| Rabbit

    %% ML Services to GPU Manager
    Content -->|Acquire GPU| GPU_Mgr
    Comment -->|Acquire GPU| GPU_Mgr
    Rec -->|Acquire GPU| GPU_Mgr

    %% GPU Manager to hardware
    GPU_Mgr -.->|Allocate| GPU0
    GPU_Mgr -.->|Allocate| GPU1

    %% Async processing
    Content -->|Jobs| Rabbit
    Comment -->|Jobs| Rabbit
    Rec -->|Jobs| Rabbit

    %% Data persistence
    Postgres -.->|Persist| Volumes
    RedisDB -.->|Persist| Volumes
    WebApp -.->|Store files| Volumes

    style WebApp fill:#e1f5ff
    style GPU_Mgr fill:#ffe1f5
    style Content fill:#f0e1ff
    style Comment fill:#f0e1ff
    style Rec fill:#f0e1ff
```

## 7. Data Flow - Multi-Dimensional Rating System

```mermaid
graph LR
    subgraph "User Input"
        Form[Review Form<br/>- DishRating 1-10<br/>- ServiceRating 1-10<br/>- CleanlinessRating 1-10<br/>- AmbianceRating 1-10<br/>- Comment<br/>- Photos]
    end

    subgraph "Validation"
        Valid[Validation Layer<br/>- Required: DishRating<br/>- Optional: Service, Cleanliness, Ambiance<br/>- Scale: 1-10]
    end

    subgraph "ViewModels"
        AddVM[AddReviewViewModel<br/>int DishRating<br/>int? ServiceRating<br/>int? CleanlinessRating<br/>int? AmbianceRating]

        ReviewVM[ReviewViewModel<br/>int DishRating<br/>int? ServiceRating<br/>int? CleanlinessRating<br/>int? AmbianceRating<br/>double AverageOverallRating]
    end

    subgraph "Database"
        DB[(Reviews Table<br/>dish_rating INT NOT NULL<br/>service_rating INT NULL<br/>cleanliness_rating INT NULL<br/>ambiance_rating INT NULL)]
    end

    subgraph "Calculations"
        Calc[Computed Properties<br/>- Rating = DishRating<br/>- AverageOverallRating<br/>&nbsp;&nbsp;= AVG of all ratings]
    end

    Form -->|Submit| Valid
    Valid -->|Maps to| AddVM
    AddVM -->|Service Layer| DB
    DB -->|Query| ReviewVM
    ReviewVM -->|Compute| Calc
    Calc -->|Display| Form

    style AddVM fill:#fff4e1
    style ReviewVM fill:#e1f5ff
    style DB fill:#f0e1ff
```

## 8. PWA Architecture

```mermaid
graph TB
    subgraph "Browser"
        Page[Web Pages]
        SW[Service Worker<br/>sw.js]
        Cache[Cache Storage<br/>- Static assets<br/>- API responses<br/>- Offline pages]
        IndexedDB[(IndexedDB<br/>Offline data)]
    end

    subgraph "Server"
        MVC[MVC Controllers<br/>Initial page load]
        API[API Controllers<br/>Data endpoints]
        Static[Static Files<br/>CSS, JS, Images]
    end

    User[User]
    Network{Network<br/>Available?}

    %% User interactions
    User -->|Navigate| Page

    %% Service Worker intercepts
    Page -->|Request| SW

    %% Network check
    SW --> Network

    %% Online flow
    Network -->|Yes| MVC
    Network -->|Yes| API
    MVC -->|HTML| SW
    API -->|JSON| SW
    Static -->|Assets| SW

    %% Offline flow
    Network -->|No| Cache
    Network -->|No| IndexedDB

    %% Cache management
    SW -->|Store| Cache
    SW -->|Store| IndexedDB
    Cache -->|Serve| Page
    IndexedDB -->|Serve| Page

    %% Background sync
    SW -.->|Sync when online| API

    style SW fill:#ffe1f5
    style Cache fill:#e1f5ff
    style Network fill:#fff4e1
```

## Architecture Benefits Summary

### Hybrid MVC + API Approach

| Feature | MVC Controllers | API Controllers | Benefit |
|---------|----------------|-----------------|---------|
| **SEO** | ✅ Full HTML | ❌ No HTML | Search engines index pages |
| **Initial Load** | ✅ Fast | ⚠️ Requires JS | Users see content immediately |
| **Interactivity** | ⚠️ Page reload | ✅ Dynamic | Smooth user experience |
| **PWA Support** | ❌ Limited | ✅ Full | Offline capability, mobile install |
| **Mobile Apps** | ❌ No | ✅ Yes | Future iOS/Android apps |
| **Bandwidth** | ⚠️ Full HTML | ✅ JSON only | Efficient data transfer |
| **State Management** | ⚠️ Server-side | ✅ Client-side | Rich interactions |

### When to Use Each

**Use MVC Controllers when:**
- Initial page loads (Home, Product pages)
- SEO is critical
- Forms with complex server-side validation
- Authentication/authorization pages
- Content-heavy pages

**Use API Controllers when:**
- Search autocomplete
- Infinite scroll
- Filtering/sorting without reload
- Real-time updates
- Mobile app data
- Third-party integrations

### Microservices Benefits

1. **Separation of Concerns**: ML models isolated from web app
2. **Independent Scaling**: Scale GPU services separately
3. **Technology Flexibility**: Python for ML, C# for web
4. **Resource Management**: Centralized GPU pooling
5. **Fault Isolation**: ML service failure doesn't crash web app
6. **Independent Deployment**: Update ML models without touching web app


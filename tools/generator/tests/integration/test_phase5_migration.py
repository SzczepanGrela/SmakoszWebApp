from unittest.mock import MagicMock, patch

from generators.phase5_reviews import ReviewsPhase
from orchestration import ExecutionContext, PhaseRegistry, PhaseStatus

class TestPhase5Metadata:

    def test_reviews_phase_metadata(self):
        phase = ReviewsPhase(blueprints_dir="blueprints")
        metadata = phase.metadata

        assert metadata.phase_id == "phase5_reviews"
        assert metadata.display_name == "Reviews Generation"

        assert len(metadata.dependencies) == 3
        assert "phase4_users" in metadata.dependencies
        assert "phase2_restaurants" in metadata.dependencies
        assert "phase3_dishes" in metadata.dependencies

        assert "reviews" in metadata.required_tables
        assert "media_assets" in metadata.required_tables

class TestPhase5Registration:

    def test_reviews_phase_registers(self):
        registry = PhaseRegistry()
        phase = ReviewsPhase()

        registry.register(phase)

        retrieved = registry.get("phase5_reviews")
        assert retrieved is phase

    def test_phase5_triple_dependency_resolution(self):
        from generators.phase1_definitions import CitiesPhase, CuisineTypesPhase, IngredientsPhase, TagsPhase
        from generators.phase2_restaurants import RestaurantsPhase
        from generators.phase3_dishes import DishesPhase
        from generators.phase4_users import UsersPhase

        registry = PhaseRegistry()

        registry.register(CitiesPhase())
        registry.register(CuisineTypesPhase())
        registry.register(IngredientsPhase())
        registry.register(TagsPhase())
        registry.register(RestaurantsPhase())
        registry.register(DishesPhase())
        registry.register(UsersPhase())
        registry.register(ReviewsPhase())

        resolved = registry.resolve_dependencies(["phase5_reviews"])

        assert len(resolved) >= 5

        cities_idx = resolved.index("phase1_cities")
        ingredients_idx = resolved.index("phase1_ingredients")
        restaurants_idx = resolved.index("phase2_restaurants")
        dishes_idx = resolved.index("phase3_dishes")
        users_idx = resolved.index("phase4_users")
        reviews_idx = resolved.index("phase5_reviews")

        assert cities_idx < restaurants_idx
        assert cities_idx < users_idx

        assert ingredients_idx < dishes_idx

        assert restaurants_idx < dishes_idx

        assert users_idx < reviews_idx
        assert restaurants_idx < reviews_idx
        assert dishes_idx < reviews_idx

class TestPhase5DependencyValidation:

    def test_phase5_requires_users(self):
        mock_db = MagicMock()

        phase = ReviewsPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        with patch("generators.phase5_reviews.generate_reviews") as mock_gen:
            mock_gen.side_effect = Exception("No users found to process!")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert "users" in str(result.error).lower()

    def test_phase5_requires_restaurants(self):
        mock_db = MagicMock()

        phase = ReviewsPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        with patch("generators.phase5_reviews.generate_reviews") as mock_gen:
            mock_gen.side_effect = Exception("No restaurants found! Phase 2 may have failed.")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert "restaurants" in str(result.error).lower()

    def test_phase5_requires_dishes(self):
        mock_db = MagicMock()

        phase = ReviewsPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        with patch("generators.phase5_reviews.generate_reviews") as mock_gen:
            mock_gen.side_effect = Exception("No dishes available for restaurant")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None

class TestPhase5ExecutionStructure:

    def test_reviews_phase_execute_structure(self):
        mock_db = MagicMock()
        mock_db.fetch_val.side_effect = [
            5000,
            750,
        ]

        phase = ReviewsPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        with patch("generators.phase5_reviews.generate_reviews"):
            result = phase.execute(context)

        assert result.phase_id == "phase5_reviews"
        assert result.status == PhaseStatus.COMPLETED
        assert result.duration_seconds >= 0

        assert "reviews" in result.entities_generated
        assert "review_photos" in result.entities_generated

        assert result.entities_generated["reviews"] == 5000
        assert result.entities_generated["review_photos"] == 750

        assert result.error is None

class TestPhase5ErrorHandling:

    def test_reviews_phase_handles_generation_failure(self):
        mock_db = MagicMock()

        phase = ReviewsPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        with patch("generators.phase5_reviews.generate_reviews") as mock_gen:
            mock_gen.side_effect = RuntimeError("Multiprocessing worker failed")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert isinstance(result.error, RuntimeError)
        assert "worker failed" in str(result.error).lower()

class TestPhase5ComplexDependencies:

    def test_full_dependency_chain(self):
        from generators.phase1_definitions import CitiesPhase, CuisineTypesPhase, IngredientsPhase, TagsPhase
        from generators.phase2_restaurants import RestaurantsPhase
        from generators.phase3_dishes import DishesPhase
        from generators.phase4_users import UsersPhase

        registry = PhaseRegistry()

        registry.register(CitiesPhase())
        registry.register(CuisineTypesPhase())
        registry.register(IngredientsPhase())
        registry.register(TagsPhase())
        registry.register(RestaurantsPhase())
        registry.register(DishesPhase())
        registry.register(UsersPhase())
        registry.register(ReviewsPhase())

        resolved = registry.resolve_dependencies(["phase5_reviews"])

        assert "phase1_cities" in resolved
        assert "phase1_ingredients" in resolved
        assert "phase2_restaurants" in resolved
        assert "phase3_dishes" in resolved
        assert "phase4_users" in resolved
        assert "phase5_reviews" in resolved

        assert resolved[-1] == "phase5_reviews"

        cities_idx = resolved.index("phase1_cities")
        restaurants_idx = resolved.index("phase2_restaurants")
        users_idx = resolved.index("phase4_users")
        assert cities_idx < restaurants_idx
        assert cities_idx < users_idx

        ingredients_idx = resolved.index("phase1_ingredients")
        dishes_idx = resolved.index("phase3_dishes")
        assert ingredients_idx < dishes_idx
        assert restaurants_idx < dishes_idx

    def test_phase5_is_deepest_in_tree(self):
        from generators.phase1_definitions import CitiesPhase, CuisineTypesPhase, IngredientsPhase, TagsPhase
        from generators.phase2_restaurants import RestaurantsPhase
        from generators.phase3_dishes import DishesPhase
        from generators.phase4_users import UsersPhase

        registry = PhaseRegistry()

        registry.register(CitiesPhase())
        registry.register(CuisineTypesPhase())
        registry.register(IngredientsPhase())
        registry.register(TagsPhase())
        registry.register(RestaurantsPhase())
        registry.register(DishesPhase())
        registry.register(UsersPhase())
        registry.register(ReviewsPhase())

        reviews_chain = registry.resolve_dependencies(["phase5_reviews"])

        users_chain = registry.resolve_dependencies(["phase4_users"])
        dishes_chain = registry.resolve_dependencies(["phase3_dishes"])
        restaurants_chain = registry.resolve_dependencies(["phase2_restaurants"])

        assert len(reviews_chain) > len(users_chain)
        assert len(reviews_chain) > len(dishes_chain)
        assert len(reviews_chain) > len(restaurants_chain)

"""
Integration test for Phase 5 migration to new architecture.

Validates that ReviewsPhase works correctly with TRIPLE dependencies
(users + dishes + restaurants) and integrates with PhaseRegistry.
"""

from unittest.mock import MagicMock, patch

from generators.phase5_reviews import ReviewsPhase
from orchestration import ExecutionContext, PhaseRegistry, PhaseStatus

class TestPhase5Metadata:
    """Test ReviewsPhase metadata."""

    def test_reviews_phase_metadata(self):
        """Test ReviewsPhase has correct metadata."""
        phase = ReviewsPhase(blueprints_dir="blueprints")
        metadata = phase.metadata

        assert metadata.phase_id == "phase5_reviews"
        assert metadata.display_name == "Reviews Generation"

        # Critical: Phase 5 has TRIPLE dependencies
        assert len(metadata.dependencies) == 3
        assert "phase4_users" in metadata.dependencies
        assert "phase2_restaurants" in metadata.dependencies
        assert "phase3_dishes" in metadata.dependencies

        assert "reviews" in metadata.required_tables
        assert "media_assets" in metadata.required_tables

class TestPhase5Registration:
    """Test Phase 5 integration with PhaseRegistry."""

    def test_reviews_phase_registers(self):
        """Test that ReviewsPhase can be registered."""
        registry = PhaseRegistry()
        phase = ReviewsPhase()

        registry.register(phase)

        retrieved = registry.get("phase5_reviews")
        assert retrieved is phase

    def test_phase5_triple_dependency_resolution(self):
        """Test that Phase 5 triple dependencies are resolved correctly."""
        from generators.phase1_definitions import CitiesPhase, IngredientsPhase
        from generators.phase2_restaurants import RestaurantsPhase
        from generators.phase3_dishes import DishesPhase
        from generators.phase4_users import UsersPhase

        registry = PhaseRegistry()

        # Register all dependencies (full chain)
        registry.register(CitiesPhase())        # Required by Restaurants + Users
        registry.register(IngredientsPhase())   # Required by Dishes
        registry.register(RestaurantsPhase())   # Direct dependency of Reviews
        registry.register(DishesPhase())        # Direct dependency of Reviews
        registry.register(UsersPhase())         # Direct dependency of Reviews
        registry.register(ReviewsPhase())

        # Resolve dependencies for Phase 5
        resolved = registry.resolve_dependencies(["phase5_reviews"])

        # Should include all dependencies in correct order
        assert len(resolved) >= 5

        # Get indices
        cities_idx = resolved.index("phase1_cities")
        ingredients_idx = resolved.index("phase1_ingredients")
        restaurants_idx = resolved.index("phase2_restaurants")
        dishes_idx = resolved.index("phase3_dishes")
        users_idx = resolved.index("phase4_users")
        reviews_idx = resolved.index("phase5_reviews")

        # Cities must come before Restaurants and Users
        assert cities_idx < restaurants_idx
        assert cities_idx < users_idx

        # Ingredients must come before Dishes
        assert ingredients_idx < dishes_idx

        # Restaurants must come before Dishes
        assert restaurants_idx < dishes_idx

        # All three direct dependencies must come before Reviews
        assert users_idx < reviews_idx
        assert restaurants_idx < reviews_idx
        assert dishes_idx < reviews_idx

class TestPhase5DependencyValidation:
    """Test Phase 5 dependency validation."""

    def test_phase5_requires_users(self):
        """Test that Phase 5 validates users dependency."""
        mock_db = MagicMock()

        phase = ReviewsPhase(blueprints_dir="blueprints")
        context = ExecutionContext(
            db=mock_db,
            config={},
            phase_registry=PhaseRegistry()
        )

        # Simulate error due to missing users
        with patch("generators.phase5_reviews.generate_reviews") as mock_gen:
            mock_gen.side_effect = Exception("No users found to process!")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert "users" in str(result.error).lower()

    def test_phase5_requires_restaurants(self):
        """Test that Phase 5 validates restaurants dependency."""
        mock_db = MagicMock()

        phase = ReviewsPhase(blueprints_dir="blueprints")
        context = ExecutionContext(
            db=mock_db,
            config={},
            phase_registry=PhaseRegistry()
        )

        # Simulate error due to missing restaurants
        with patch("generators.phase5_reviews.generate_reviews") as mock_gen:
            mock_gen.side_effect = Exception("No restaurants found! Phase 2 may have failed.")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert "restaurants" in str(result.error).lower()

    def test_phase5_requires_dishes(self):
        """Test that Phase 5 validates dishes dependency (indirectly)."""
        mock_db = MagicMock()

        phase = ReviewsPhase(blueprints_dir="blueprints")
        context = ExecutionContext(
            db=mock_db,
            config={},
            phase_registry=PhaseRegistry()
        )

        # Simulate error due to missing dishes (would occur during review generation)
        with patch("generators.phase5_reviews.generate_reviews") as mock_gen:
            mock_gen.side_effect = Exception("No dishes available for restaurant")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None

class TestPhase5ExecutionStructure:
    """Test Phase 5 execution structure."""

    def test_reviews_phase_execute_structure(self):
        """Test that execute() returns proper PhaseResult structure."""
        mock_db = MagicMock()
        mock_db.fetch_val.side_effect = [
            5000,  # reviews count
            750,   # review photos (media_assets) count
        ]

        phase = ReviewsPhase(blueprints_dir="blueprints")
        context = ExecutionContext(
            db=mock_db,
            config={},
            phase_registry=PhaseRegistry()
        )

        # Mock the generate_reviews function
        with patch("generators.phase5_reviews.generate_reviews"):
            result = phase.execute(context)

        # Verify result structure
        assert result.phase_id == "phase5_reviews"
        assert result.status == PhaseStatus.COMPLETED
        assert result.duration_seconds >= 0

        # Check entities generated
        assert "reviews" in result.entities_generated
        assert "review_photos" in result.entities_generated

        assert result.entities_generated["reviews"] == 5000
        assert result.entities_generated["review_photos"] == 750

        assert result.error is None

class TestPhase5ErrorHandling:
    """Test Phase 5 error handling."""

    def test_reviews_phase_handles_generation_failure(self):
        """Test that ReviewsPhase handles generation failures gracefully."""
        mock_db = MagicMock()

        phase = ReviewsPhase(blueprints_dir="blueprints")
        context = ExecutionContext(
            db=mock_db,
            config={},
            phase_registry=PhaseRegistry()
        )

        # Mock generate_reviews to raise an exception
        with patch("generators.phase5_reviews.generate_reviews") as mock_gen:
            mock_gen.side_effect = RuntimeError("Multiprocessing worker failed")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert isinstance(result.error, RuntimeError)
        assert "worker failed" in str(result.error).lower()

class TestPhase5ComplexDependencies:
    """Test Phase 5 complex dependency scenarios."""

    def test_full_dependency_chain(self):
        """Test complete dependency chain for Phase 5 (most complex so far)."""
        from generators.phase1_definitions import CitiesPhase, IngredientsPhase
        from generators.phase2_restaurants import RestaurantsPhase
        from generators.phase3_dishes import DishesPhase
        from generators.phase4_users import UsersPhase

        registry = PhaseRegistry()

        # Register full chain (6 phases total)
        registry.register(CitiesPhase())
        registry.register(IngredientsPhase())
        registry.register(RestaurantsPhase())
        registry.register(DishesPhase())
        registry.register(UsersPhase())
        registry.register(ReviewsPhase())

        # Request only Phase 5
        resolved = registry.resolve_dependencies(["phase5_reviews"])

        # Should automatically include entire dependency tree
        assert "phase1_cities" in resolved
        assert "phase1_ingredients" in resolved
        assert "phase2_restaurants" in resolved
        assert "phase3_dishes" in resolved
        assert "phase4_users" in resolved
        assert "phase5_reviews" in resolved

        # Verify Reviews is last (depends on everything)
        assert resolved[-1] == "phase5_reviews"

        # Verify Cities comes before Restaurants and Users
        cities_idx = resolved.index("phase1_cities")
        restaurants_idx = resolved.index("phase2_restaurants")
        users_idx = resolved.index("phase4_users")
        assert cities_idx < restaurants_idx
        assert cities_idx < users_idx

        # Verify Ingredients and Restaurants come before Dishes
        ingredients_idx = resolved.index("phase1_ingredients")
        dishes_idx = resolved.index("phase3_dishes")
        assert ingredients_idx < dishes_idx
        assert restaurants_idx < dishes_idx

    def test_phase5_is_deepest_in_tree(self):
        """Test that Phase 5 has the deepest dependency tree so far."""
        from generators.phase1_definitions import CitiesPhase, IngredientsPhase
        from generators.phase2_restaurants import RestaurantsPhase
        from generators.phase3_dishes import DishesPhase
        from generators.phase4_users import UsersPhase

        registry = PhaseRegistry()

        # Register all phases
        registry.register(CitiesPhase())
        registry.register(IngredientsPhase())
        registry.register(RestaurantsPhase())
        registry.register(DishesPhase())
        registry.register(UsersPhase())
        registry.register(ReviewsPhase())

        # Resolve for Reviews
        reviews_chain = registry.resolve_dependencies(["phase5_reviews"])

        # Resolve for other phases
        users_chain = registry.resolve_dependencies(["phase4_users"])
        dishes_chain = registry.resolve_dependencies(["phase3_dishes"])
        restaurants_chain = registry.resolve_dependencies(["phase2_restaurants"])

        # Phase 5 should have the longest dependency chain
        assert len(reviews_chain) > len(users_chain)
        assert len(reviews_chain) > len(dishes_chain)
        assert len(reviews_chain) > len(restaurants_chain)

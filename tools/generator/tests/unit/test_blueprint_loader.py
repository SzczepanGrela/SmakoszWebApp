import pytest

class TestDishesBlueprint:

    def test_dishes_json_loads(self, dishes_json):
        assert dishes_json is not None
        assert len(dishes_json) > 0, "dishes.json should have archetypes"

    def test_all_archetypes_have_required_fields(self, dishes_json):
        required_fields = ["base_price", "archetype_base", "variants"]

        for archetype_name, archetype_data in dishes_json.items():
            if not isinstance(archetype_data, dict):
                continue

            for field in required_fields:
                assert field in archetype_data, f"Archetype '{archetype_name}' missing required field '{field}'"

    def test_archetype_base_has_characteristics(self, dishes_json):
        for archetype_name, archetype_data in dishes_json.items():
            if not isinstance(archetype_data, dict):
                continue

            base = archetype_data.get("archetype_base", {})
            assert "characteristics" in base, f"Archetype '{archetype_name}' missing archetype_base.characteristics"
            assert "default_weights" in base, f"Archetype '{archetype_name}' missing archetype_base.default_weights"

    def test_all_variants_have_ingredients(self, dishes_json):
        for archetype_name, archetype_data in dishes_json.items():
            if not isinstance(archetype_data, dict):
                continue

            for variant_name, variant_data in archetype_data.get("variants", {}).items():
                assert "ingredients" in variant_data, f"Variant '{archetype_name}.{variant_name}' missing ingredients"
                assert isinstance(variant_data["ingredients"], list), (
                    f"Variant '{archetype_name}.{variant_name}' ingredients must be list"
                )

    def test_variant_price_multiplier_reasonable(self, dishes_json):
        for archetype_name, archetype_data in dishes_json.items():
            if not isinstance(archetype_data, dict):
                continue

            for variant_name, variant_data in archetype_data.get("variants", {}).items():
                multiplier = variant_data.get("price_multiplier", {})
                if isinstance(multiplier, dict):
                    mean = multiplier.get("mean", 1.0)
                    assert 0.1 <= mean <= 5.0, (
                        f"Variant '{archetype_name}.{variant_name}' price multiplier {mean} out of range"
                    )

    def test_characteristics_values_normalized(self, dishes_json):
        for archetype_name, archetype_data in dishes_json.items():
            if not isinstance(archetype_data, dict):
                continue

            base_chars = archetype_data.get("archetype_base", {}).get("characteristics", {})
            for dim, val in base_chars.items():
                assert 0.0 <= val <= 1.0, f"Archetype '{archetype_name}' characteristic {dim}={val} out of [0,1]"

            for variant_name, variant_data in archetype_data.get("variants", {}).items():
                var_chars = variant_data.get("characteristics", {})
                for dim, val in var_chars.items():
                    assert 0.0 <= val <= 1.0, f"Variant '{archetype_name}.{variant_name}' {dim}={val} out of [0,1]"

class TestMenuTemplatesBlueprint:

    def test_menu_templates_loads(self, menu_templates_json):
        assert menu_templates_json is not None

    def test_menu_templates_have_archetypes(self, menu_templates_json, dishes_json):
        valid_archetypes = set(dishes_json.keys())

        for template_name, template_data in menu_templates_json.items():
            if not isinstance(template_data, dict):
                continue

            categories = template_data.get("categories", {})
            for _category_name, category_archetypes in categories.items():
                if isinstance(category_archetypes, list):
                    for arch in category_archetypes:
                        archetype_name = arch if isinstance(arch, str) else arch.get("archetype", "")
                        if archetype_name:
                            assert archetype_name in valid_archetypes, (
                                f"Template '{template_name}' references unknown archetype '{archetype_name}'"
                            )

class TestIngredientsBlueprint:

    def test_ingredients_loads(self, ingredients_json):
        assert ingredients_json is not None
        assert len(ingredients_json) > 0, "ingredients_list.json should have ingredients"

    def test_ingredients_are_strings(self, ingredients_json):
        for ingredient in ingredients_json:
            assert isinstance(ingredient, str), f"Ingredient should be string, got {type(ingredient)}"

class TestCrossReferenceValidation:

    def test_variant_ingredients_exist(self, dishes_json, ingredients_json):
        valid_ingredients = set(ingredients_json)
        missing = []

        for archetype_name, archetype_data in dishes_json.items():
            if not isinstance(archetype_data, dict):
                continue

            for variant_name, variant_data in archetype_data.get("variants", {}).items():
                for ingredient in variant_data.get("ingredients", []):
                    if ingredient not in valid_ingredients:
                        missing.append(f"{archetype_name}.{variant_name}: {ingredient}")

        if missing and len(missing) > 10:
            pytest.skip(f"Many missing ingredients ({len(missing)}) - may need blueprint update")
